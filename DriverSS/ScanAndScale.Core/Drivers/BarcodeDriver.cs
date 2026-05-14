// ============================================================
// File: Drivers/BarcodeDriver.cs
// Mục đích: Driver đọc barcode từ máy quét Zebra (Zebra CoreScanner SDK)
//
// YÊU CẦU:
//   1. Cài đặt Zebra Scanner SDK trên máy tính.
//   2. Copy file "Interop.CoreScanner.dll" vào thư mục "lib\" của project này.
//   3. Build → ZEBRA_SCANNER symbol sẽ được tự động định nghĩa.
//
// Nếu không có SDK, driver sẽ vẫn compile được nhưng không hoạt động.
// Dùng symbol #if ZEBRA_SCANNER để kiểm tra.
//
// QUAN TRỌNG VỀ THREAD:
//   Sự kiện DataValueChanged được gọi từ background thread của Zebra SDK.
//   Trong WPF: dùng Application.Current.Dispatcher.Invoke(...)
//   Trong WinForms: dùng control.Invoke(...)
// ============================================================

#if ZEBRA_SCANNER
using CoreScanner; // Namespace của Interop.CoreScanner.dll
#endif

using ScanAndScale.Core.Models;
using System.Diagnostics;
using System.Linq;  // Cần cho Array.Contains() khi lọc Scanner ID
using System.Xml;

namespace ScanAndScale.Core.Drivers
{
    /// <summary>
    /// Driver đọc barcode từ máy quét Zebra sử dụng Zebra CoreScanner SDK.
    /// <para>
    /// Hoạt động theo mô hình Singleton — toàn ứng dụng chỉ có một instance.
    /// Mọi màn hình/control đều đăng ký vào event của instance này.
    /// </para>
    /// <para>
    /// Cách dùng:
    /// <code>
    ///   var driver = BarcodeDriver.Instance;
    ///   driver.Initialize(new BarcodeConfig { Enable = true });
    ///   driver.DataValueChanged += (s, e) => {
    ///     // e.NewValue.Value là chuỗi barcode vừa quét
    ///     Console.WriteLine(e.NewValue.Value);
    ///   };
    ///   // Khi thoát:
    ///   driver.Dispose();
    /// </code>
    /// </para>
    /// </summary>
    public sealed class BarcodeDriver : IDisposable
    {
        // ===================================================
        // SINGLETON PATTERN
        // Đảm bảo chỉ có một instance duy nhất trong ứng dụng
        // vì Zebra SDK chỉ cho phép mở một kết nối tại một thời điểm.
        // ===================================================
        private static BarcodeDriver? _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>
        /// Lấy instance duy nhất của BarcodeDriver (Singleton).
        /// </summary>
        public static BarcodeDriver Instance
        {
            get
            {
                // Double-check locking để thread-safe khi tạo instance
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                            _instance = new BarcodeDriver();
                    }
                }
                return _instance;
            }
        }

        // ===================================================
        // FIELDS — Biến nội bộ của driver
        // ===================================================

#if ZEBRA_SCANNER
        // Object kết nối chính đến Zebra CoreScanner SDK
        private CCoreScanner? _coreScanner;
#endif

        // Config hiện tại của driver
        private BarcodeConfig? _config;

        // Trạng thái khởi tạo
        private bool _isInitialized = false;

        // Đã dispose chưa (tránh double-dispose)
        private bool _disposed = false;

        // DataValue hiện tại — lưu giá trị barcode gần nhất
        private DataValue _currentDataValue = new DataValue(DriverStatus.Unknown, null);

        // ===================================================
        // EVENTS — Sự kiện thông báo khi có dữ liệu mới
        // ===================================================

        // Backing field cho event (thread-safe add/remove)
        private EventHandler<DataValueChangedEventArgs>? _dataValueChanged;

        /// <summary>
        /// Sự kiện kích hoạt khi quét được barcode mới, hoặc trạng thái thay đổi.
        /// <para>
        /// ⚠️ THREAD WARNING: Event này được gọi từ background thread của Zebra SDK.
        /// Trong WPF: cần Dispatcher.Invoke(); Trong WinForms: cần Control.Invoke().
        /// </para>
        /// </summary>
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }

        // ===================================================
        // PROPERTIES — Thuộc tính công khai
        // ===================================================

        /// <summary>Giá trị barcode hiện tại (DataValue gần nhất).</summary>
        public DataValue CurrentValue => _currentDataValue;

        /// <summary>Driver đã được khởi tạo và đang hoạt động không.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>Zebra SDK có được cài đặt và DLL có sẵn không.</summary>
        public bool IsZebraSdkAvailable
        {
            get
            {
#if ZEBRA_SCANNER
                return true;
#else
                return false;
#endif
            }
        }

        // ===================================================
        // CONSTRUCTOR — Private (Singleton)
        // ===================================================
        private BarcodeDriver() { }

        // ===================================================
        // KHỞI TẠO (Initialize)
        // Bước 1: Gọi trước tiên để kết nối với Zebra SDK
        // ===================================================

        /// <summary>
        /// Khởi tạo kết nối với Zebra CoreScanner SDK và bắt đầu lắng nghe barcode.
        /// <para>Gọi một lần khi ứng dụng khởi động.</para>
        /// </summary>
        /// <param name="config">Cấu hình barcode driver. Null = dùng mặc định.</param>
        /// <returns>true nếu khởi tạo thành công, false nếu thất bại (SDK không có, v.v.).</returns>
        public bool Initialize(BarcodeConfig? config = null)
        {
            // Lưu config (dùng mặc định nếu không cung cấp)
            _config = config ?? new BarcodeConfig();

            // Kiểm tra enable
            if (!_config.Enable)
            {
                LogInfo("BarcodeDriver bị vô hiệu hóa (Enable=false).");
                return false;
            }

            // Nếu đã khởi tạo rồi thì bỏ qua
            if (_isInitialized)
            {
                LogInfo("BarcodeDriver đã được khởi tạo rồi.");
                return true;
            }

#if ZEBRA_SCANNER
            // Bước 1: Tạo instance CCoreScanner (đối tượng kết nối chính của Zebra SDK)
            return InitializeZebraScanner();
#else
            // Không có Zebra SDK — log cảnh báo
            LogInfo("CẢNH BÁO: Zebra CoreScanner SDK không có sẵn. " +
                    "Copy Interop.CoreScanner.dll vào thư mục lib\\ để bật tính năng này.");
            return false;
#endif
        }

#if ZEBRA_SCANNER
        /// <summary>
        /// Thực hiện khởi tạo Zebra Scanner SDK (chỉ compile khi có DLL).
        /// </summary>
        private bool InitializeZebraScanner()
        {
            try
            {
                // Bước 2: Tạo đối tượng CCoreScanner — điểm kết nối chính với SDK
                _coreScanner = new CCoreScanner();

                // Bước 3: Chỉ định loại scanner muốn dùng
                //   scannerTypes[0] = 1 → Tất cả loại scanner
                //   scannerTypes[0] = 2 → Chỉ USB scanner
                short[] scannerTypes = { 1 };
                short numberOfTypes = 1;
                int status;

                // Mở kết nối với SDK
                _coreScanner.Open(0, scannerTypes, numberOfTypes, out status);

                if (status != 0)
                {
                    LogInfo($"Zebra SDK Open thất bại. Status code: {status}");
                    return false;
                }

                // Bước 4: Lấy danh sách scanner đang kết nối (để debug)
                short numberOfScanners;
                int[] connectedScannerIds = new int[255];
                string outXml;
                _coreScanner.GetScanners(out numberOfScanners, connectedScannerIds, out outXml, out status);
                LogInfo($"Số scanner đang kết nối: {numberOfScanners}");

                // Bước 5: Đăng ký xử lý sự kiện BarcodeEvent từ SDK
                _coreScanner.BarcodeEvent += new _ICoreScannerEvents_BarcodeEventEventHandler(OnZebraBarcodeEvent);

                // Bước 6: Subscribe event qua ExecCommand (opcode 1001 = Subscribe Events)
                //   arg-int đầu: số lượng event type
                //   arg-int sau: mã event (1 = Barcode Event)
                int opcode = 1001;
                string inXml = "<inArgs><cmdArgs><arg-int>1</arg-int><arg-int>1</arg-int></cmdArgs></inArgs>";
                _coreScanner.ExecCommand(opcode, ref inXml, out outXml, out status);

                _isInitialized = true;
                LogInfo("Zebra BarcodeDriver khởi tạo thành công.");
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "BarcodeDriver.Initialize");
                SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                return false;
            }
        }

        /// <summary>
        /// Callback được gọi bởi Zebra SDK khi scanner đọc được barcode.
        /// Chạy trên thread của SDK, không phải UI thread!
        /// </summary>
        /// <param name="eventType">Loại sự kiện (luôn là 1 = Barcode Event).</param>
        /// <param name="pscanData">Dữ liệu barcode ở dạng XML.</param>
        private void OnZebraBarcodeEvent(short eventType, ref string pscanData)
        {
            try
            {
                // Bước 1: Parse XML nhận được từ SDK
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(pscanData);

                // Bước 2: Lọc theo Scanner ID nếu config yêu cầu
                var scannerIdNodes = xmlDoc.GetElementsByTagName("scannerID");
                if (_config?.ScannerIdFilter != null && _config.ScannerIdFilter.Length > 0)
                {
                    var scannerId = int.Parse(scannerIdNodes[0]?.InnerText ?? "0");
                    if (!_config.ScannerIdFilter.Contains(scannerId))
                        return; // Không phải scanner cần lắng nghe
                }

                // Bước 3: Lấy dữ liệu barcode từ XML (dưới dạng hex ASCII)
                var datalabelNodes = xmlDoc.GetElementsByTagName("datalabel");
                if (datalabelNodes.Count == 0) return;

                // Bước 4: Chuyển hex string thành chuỗi barcode thực sự
                string barcodeValue = HexAsciiToString(datalabelNodes[0]!.InnerText);

                // Bước 5: Cập nhật giá trị và bắn sự kiện
                SetDataValue(new DataValue(DriverStatus.Connected, barcodeValue));
                LogInfo($"Barcode đọc được: {barcodeValue}");
            }
            catch (Exception ex)
            {
                LogError(ex, "OnZebraBarcodeEvent");
                SetDataValue(new DataValue(DriverStatus.Disconnected, null));
            }
        }

        /// <summary>
        /// Chuyển chuỗi hex ASCII (định dạng Zebra SDK trả về) sang chuỗi text.
        /// Ví dụ: "41 42 43" → "ABC"
        /// </summary>
        private static string HexAsciiToString(string hexString)
        {
            var result = "";
            // Tách từng byte hex, chuyển sang ký tự tương ứng
            foreach (var hex in hexString.Split(' '))
            {
                if (string.IsNullOrWhiteSpace(hex)) continue;
                int charCode = Convert.ToInt32(hex.Trim(), 16);
                result += (char)charCode;
            }
            return result;
        }
#endif

        // ===================================================
        // HÀM NỘI BỘ — Cập nhật DataValue và bắn event
        // ===================================================

        /// <summary>
        /// Cập nhật giá trị hiện tại và bắn sự kiện DataValueChanged.
        /// </summary>
        private void SetDataValue(DataValue newValue)
        {
            var oldValue = _currentDataValue;
            _currentDataValue = newValue;

            // Gọi tất cả subscribers đã đăng ký
            _dataValueChanged?.Invoke(this, new DataValueChangedEventArgs(oldValue, newValue));
        }

        // ===================================================
        // DISPOSE — Giải phóng tài nguyên khi không dùng nữa
        // ===================================================

        /// <summary>
        /// Đóng kết nối Zebra SDK và giải phóng tài nguyên.
        /// Gọi khi thoát ứng dụng hoặc không cần dùng barcode nữa.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

#if ZEBRA_SCANNER
            try
            {
                if (_coreScanner != null)
                {
                    // Hủy đăng ký sự kiện trước khi đóng
                    _coreScanner.BarcodeEvent -= OnZebraBarcodeEvent;

                    // Đóng kết nối với SDK
                    int status;
                    _coreScanner.Close(0, out status);
                    _coreScanner = null;
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "BarcodeDriver.Dispose");
            }
#endif
            _isInitialized = false;
            _instance = null; // Reset singleton để có thể khởi tạo lại
            LogInfo("BarcodeDriver đã được dispose.");
        }

        // Shortcut log methods
        private static void LogInfo(string msg) => Debug.WriteLine($"[BarcodeDriver] {msg}");
        private static void LogError(Exception ex, string ctx) => Helpers.DriverHelper.LogError(ex, ctx);
    }
}
