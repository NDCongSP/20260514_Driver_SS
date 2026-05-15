// ============================================================
// File: ViewModels/MainViewModel.cs
// Mục đích: ViewModel chính — cầu nối giữa UI (MainWindow.xaml)
//           và thư viện ScanAndScale.Core (Barcode/RFID/Scale drivers).
//
// LUỒNG DỮ LIỆU:
//   Device → Driver (background thread)
//          → Event DataValueChanged
//          → Dispatcher.Invoke (về UI thread)
//          → Property thay đổi
//          → WPF Binding tự cập nhật UI
//
// CÁCH DÙNG:
//   1. Tạo instance MainViewModel trong MainWindow
//   2. Set DataContext = viewModel
//   3. Gọi InitializeAllDrivers() sau khi window loaded
//   4. Gọi DisposeAllDrivers() khi window đóng
// ============================================================

using ScanAndScale.Core.Drivers;
using ScanAndScale.Core.Models;
using System.Windows;

namespace WpfSample.ViewModels
{
    /// <summary>
    /// ViewModel chính của ứng dụng WPF mẫu.
    /// Quản lý 3 drivers: Barcode, RFID, Scale.
    /// </summary>
    public class MainViewModel : BaseViewModel, IDisposable
    {
        // ===================================================
        // FIELDS — Driver instances
        // ===================================================

        // Barcode driver (Singleton)
        private readonly BarcodeDriver _barcodeDriver = BarcodeDriver.Instance;

        // RFID driver (Singleton)
        private readonly RfidDriver _rfidDriver = RfidDriver.Instance;

        // Scale driver (không phải Singleton — có thể có nhiều cân)
        private ScaleDriver? _scaleDriver;

        // ===================================================
        // BACKING FIELDS cho các Properties
        // ===================================================

        // --- Barcode ---
        private string _barcodeValue = "";
        private DriverStatus _barcodeStatus = DriverStatus.Unknown;
        private string _barcodeLog = "";

        // --- RFID ---
        private string _rfidValue = "";
        private DriverStatus _rfidStatus = DriverStatus.Unknown;
        private string _rfidLog = "";

        // --- Scale ---
        private double _scaleValue = 0;
        private string _scaleUnit = "KG";
        private bool _scaleStable = false;
        private bool _scaleTare = false;
        private DriverStatus _scaleStatus = DriverStatus.Unknown;
        private string _scaleLog = "";

        // --- UI State ---
        private bool _isInitialized = false;
        private string _statusMessage = "Chưa khởi tạo. Nhấn 'Kết nối' để bắt đầu.";

        // ===================================================
        // PROPERTIES — BARCODE
        // WPF UI sẽ binding vào các properties này
        // ===================================================

        /// <summary>
        /// Chuỗi barcode vừa quét được.
        /// Binding: {Binding BarcodeValue}
        /// </summary>
        public string BarcodeValue
        {
            get => _barcodeValue;
            private set => SetProperty(ref _barcodeValue, value);
        }

        /// <summary>
        /// Trạng thái kết nối Barcode Driver.
        /// Binding: {Binding BarcodeStatus}
        /// </summary>
        public DriverStatus BarcodeStatus
        {
            get => _barcodeStatus;
            private set
            {
                if (SetProperty(ref _barcodeStatus, value))
                    OnPropertyChanged(nameof(BarcodeStatusText));
            }
        }

        /// <summary>Chuỗi hiển thị trạng thái Barcode.</summary>
        public string BarcodeStatusText => GetStatusText(_barcodeStatus);

        /// <summary>Log thao tác barcode.</summary>
        public string BarcodeLog
        {
            get => _barcodeLog;
            private set => SetProperty(ref _barcodeLog, value);
        }

        // ===================================================
        // PROPERTIES — RFID
        // ===================================================

        /// <summary>
        /// Mã số thẻ RFID vừa quét được.
        /// Binding: {Binding RfidValue}
        /// </summary>
        public string RfidValue
        {
            get => _rfidValue;
            private set => SetProperty(ref _rfidValue, value);
        }

        /// <summary>
        /// Trạng thái kết nối RFID Driver.
        /// Binding: {Binding RfidStatus}
        /// </summary>
        public DriverStatus RfidStatus
        {
            get => _rfidStatus;
            private set
            {
                if (SetProperty(ref _rfidStatus, value))
                    OnPropertyChanged(nameof(RfidStatusText));
            }
        }

        /// <summary>Chuỗi hiển thị trạng thái RFID.</summary>
        public string RfidStatusText => GetStatusText(_rfidStatus);

        /// <summary>Log thao tác RFID.</summary>
        public string RfidLog
        {
            get => _rfidLog;
            private set => SetProperty(ref _rfidLog, value);
        }

        // ===================================================
        // PROPERTIES — SCALE
        // ===================================================

        /// <summary>
        /// Giá trị cân (kg, sau hiệu chỉnh CalibZero và CalibGain).
        /// Binding: {Binding ScaleValue, StringFormat={}{0:F3}}
        /// </summary>
        public double ScaleValue
        {
            get => _scaleValue;
            private set
            {
                if (SetProperty(ref _scaleValue, value))
                    OnPropertyChanged(nameof(ScaleDisplayText));
            }
        }

        /// <summary>Đơn vị cân (KG, G, TON).</summary>
        public string ScaleUnit
        {
            get => _scaleUnit;
            private set
            {
                if (SetProperty(ref _scaleUnit, value))
                    OnPropertyChanged(nameof(ScaleDisplayText));
            }
        }

        /// <summary>
        /// Giá trị cân đã format kèm đơn vị — dùng trực tiếp cho TextBox.Text.
        /// Ví dụ: "3.142 KG"
        /// Tránh dùng MultiBinding StringFormat vì có thể render sai khi
        /// hai binding update không đồng thời (timing mismatch).
        /// </summary>
        public string ScaleDisplayText => $"{_scaleValue:F3} {_scaleUnit}";

        /// <summary>Cân có ổn định không (Stable flag).</summary>
        public bool ScaleStable
        {
            get => _scaleStable;
            private set => SetProperty(ref _scaleStable, value);
        }

        /// <summary>Cân đang tare không.</summary>
        public bool ScaleTare
        {
            get => _scaleTare;
            private set => SetProperty(ref _scaleTare, value);
        }

        /// <summary>Trạng thái kết nối Scale Driver.</summary>
        public DriverStatus ScaleStatus
        {
            get => _scaleStatus;
            private set
            {
                if (SetProperty(ref _scaleStatus, value))
                    OnPropertyChanged(nameof(ScaleStatusText));
            }
        }

        /// <summary>Chuỗi hiển thị trạng thái Scale.</summary>
        public string ScaleStatusText => GetStatusText(_scaleStatus);

        /// <summary>Log thao tác cân.</summary>
        public string ScaleLog
        {
            get => _scaleLog;
            private set => SetProperty(ref _scaleLog, value);
        }

        // ===================================================
        // PROPERTIES — TRẠNG THÁI CHUNG
        // ===================================================

        /// <summary>Thông báo trạng thái chung hiển thị ở thanh dưới.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>Đã khởi tạo xong chưa (dùng để enable/disable nút).</summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            private set
            {
                if (SetProperty(ref _isInitialized, value))
                {
                    // Thông báo WPF cập nhật lại các Command (enable/disable nút)
                    RelayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // ===================================================
        // COMMANDS — Các nút trong UI
        // ===================================================

        /// <summary>Command cho nút "Kết nối tất cả".</summary>
        public RelayCommand ConnectAllCommand { get; }

        /// <summary>Command cho nút "Ngắt kết nối".</summary>
        public RelayCommand DisconnectAllCommand { get; }

        /// <summary>Command cho nút "Kết nối lại RFID".</summary>
        public RelayCommand ReconnectRfidCommand { get; }

        /// <summary>Command cho nút "Xóa log".</summary>
        public RelayCommand ClearLogCommand { get; }

        // ===================================================
        // CẤU HÌNH MẪU — Thay đổi theo thực tế của bạn
        // ===================================================

        // Cấu hình Barcode (Zebra Scanner SDK)
        private readonly BarcodeConfig _barcodeConfig = new BarcodeConfig
        {
            Enable = true,     // Bật driver barcode
            ReadOnly = false   // Cho phép nhập tay nếu cần
        };

        // Cấu hình RFID (SerialPort)
        private readonly RfidConfig _rfidConfig = new RfidConfig
        {
            Enable = true,               // Bật driver RFID
            AutoFindCom = true,          // Tự tìm cổng COM
            DeviceCaption = "Pongee",    // Tên thiết bị trong Device Manager
            DeviceManufacturer = "Prolific", // Nhà sản xuất chip USB-Serial
            // ComPort = "COM3",         // Dùng cổng cụ thể nếu AutoFindCom = false
            BaudRate = 9600              // Tốc độ baud của thiết bị RFID
        };

        // Cấu hình Scale (TCP/IP)
        private readonly ScaleConfig _scaleConfig = new ScaleConfig
        {
            Enable = true,
            IP = "192.168.80.237",    // ← Thay bằng IP thực của cân
            Port = 23,                 // Cổng Telnet của cân
            // Dùng ScaleModelNames thay cho string thô → tránh typo, IntelliSense hỗ trợ
            // Các lựa chọn: ScaleModelNames.DIGI, IND_KG, Vibra_SJ6200, Vibra_HAW30, SampleReading
            // Scale_DIGI.dll đã được nhúng vào ScanAndScale.Core.dll — không cần file bên ngoài
            ModelName = ScaleModelNames.Vibra_HAW30,
            TimeScanMs = 400,          // Đọc mỗi 400ms
            CalibZero = 0.0,           // Hiệu chỉnh offset
            CalibGain = 1.0,           // Hệ số nhân
            DecimalNum = 3,            // Số chữ số thập phân
            CheckStable = false,       // Không yêu cầu stable
            CheckTare = false          // Không kiểm tra tare
        };

        // ===================================================
        // CONSTRUCTOR
        // ===================================================

        public MainViewModel()
        {
            // Khởi tạo Commands
            ConnectAllCommand = new RelayCommand(
                execute: InitializeAllDrivers,
                canExecute: () => !_isInitialized
            );

            DisconnectAllCommand = new RelayCommand(
                execute: DisposeAllDrivers,
                canExecute: () => _isInitialized
            );

            ReconnectRfidCommand = new RelayCommand(
                execute: () => _rfidDriver.Reconnect(),
                canExecute: () => _isInitialized
            );

            ClearLogCommand = new RelayCommand(
                execute: () =>
                {
                    BarcodeLog = "";
                    RfidLog = "";
                    ScaleLog = "";
                }
            );
        }

        // ===================================================
        // KHỞI TẠO TẤT CẢ DRIVERS
        // ===================================================

        /// <summary>
        /// Khởi tạo và kết nối tất cả drivers (Barcode, RFID, Scale).
        /// Gọi hàm này sau khi MainWindow đã loaded.
        /// </summary>
        public void InitializeAllDrivers()
        {
            StatusMessage = "Đang khởi tạo drivers...";

            // ------------------------------------------------
            // BƯỚC 1: Đăng ký events TRƯỚC khi Initialize
            // Để không bỏ sót dữ liệu đến ngay sau khi init
            // ------------------------------------------------

            // Đăng ký event Barcode
            _barcodeDriver.DataValueChanged += OnBarcodeDataChanged;

            // Đăng ký event RFID
            _rfidDriver.DataValueChanged += OnRfidDataChanged;

            // ------------------------------------------------
            // BƯỚC 2: Khởi tạo Barcode Driver
            // ------------------------------------------------
            bool barcodeOk = _barcodeDriver.Initialize(_barcodeConfig);
            BarcodeStatus = barcodeOk ? DriverStatus.Connected : DriverStatus.Disconnected;
            AppendLog(ref _barcodeLog, nameof(BarcodeLog),
                barcodeOk ? "✓ Barcode driver khởi tạo thành công." :
                            "✗ Barcode driver thất bại (kiểm tra Zebra SDK và Interop.CoreScanner.dll).");

            // ------------------------------------------------
            // BƯỚC 3: Khởi tạo RFID Driver
            // ------------------------------------------------
            bool rfidOk = _rfidDriver.Initialize(_rfidConfig);
            RfidStatus = rfidOk ? DriverStatus.Connected : DriverStatus.Disconnected;
            AppendLog(ref _rfidLog, nameof(RfidLog),
                rfidOk ? $"✓ RFID driver kết nối thành công tại {_rfidDriver.CurrentComPort}." :
                         "✗ RFID driver thất bại (kiểm tra kết nối thiết bị RFID).");

            // ------------------------------------------------
            // BƯỚC 4: Khởi tạo Scale Driver
            // Scale driver không phải Singleton → tạo instance mới
            // ------------------------------------------------
            // Scale DLL (Scale_DIGI.dll, ...) đã nhúng trong ScanAndScale.Core.dll —
            // ScaleDriver.Initialize() tự load từ EmbeddedResource, không cần file bên ngoài.
            _scaleDriver = new ScaleDriver();
            _scaleDriver.DataValueChanged += OnScaleDataChanged;
            _scaleDriver.Initialize(_scaleConfig);
            AppendLog(ref _scaleLog, nameof(ScaleLog),
                $"Đang kết nối cân {_scaleConfig.ModelName} tại {_scaleConfig.IP}:{_scaleConfig.Port}...");

            IsInitialized = true;
            StatusMessage = "Tất cả drivers đã được khởi tạo. Đang lắng nghe dữ liệu...";
        }

        // ===================================================
        // XỬ LÝ SỰ KIỆN TỪ DRIVERS
        // Mỗi event chạy trên background thread!
        // Phải Dispatcher.Invoke để cập nhật UI
        // ===================================================

        /// <summary>
        /// Xử lý khi Barcode Driver nhận được barcode mới.
        /// ⚠️ Chạy trên background thread của Zebra SDK!
        /// </summary>
        private void OnBarcodeDataChanged(object? sender, DataValueChangedEventArgs e)
        {
            // Phải về UI thread mới được cập nhật properties (WPF requirement)
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Lấy giá trị mới từ event args
                var newData = e.NewValue;

                // Cập nhật trạng thái kết nối
                BarcodeStatus = newData.DriverStatus;

                // Chỉ cập nhật giá trị khi thiết bị connected và có dữ liệu
                if (newData.IsValid)
                {
                    BarcodeValue = newData.Value?.ToString() ?? "";
                    AppendLog(ref _barcodeLog, nameof(BarcodeLog),
                        $"Barcode: {BarcodeValue}");
                }
            });
        }

        /// <summary>
        /// Xử lý khi RFID Driver đọc được thẻ mới.
        /// ⚠️ Chạy trên ThreadPool thread (SerialPort.DataReceived)!
        /// </summary>
        private void OnRfidDataChanged(object? sender, DataValueChangedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var newData = e.NewValue;

                // Cập nhật trạng thái kết nối
                RfidStatus = newData.DriverStatus;

                if (newData.IsValid)
                {
                    RfidValue = newData.Value?.ToString() ?? "";
                    AppendLog(ref _rfidLog, nameof(RfidLog),
                        $"RFID: {RfidValue}");
                }
                else if (newData.DriverStatus == DriverStatus.Disconnected)
                {
                    RfidValue = "";
                    AppendLog(ref _rfidLog, nameof(RfidLog), "RFID: Mất kết nối.");
                }
            });
        }

        /// <summary>
        /// Xử lý khi Scale Driver đọc được giá trị mới.
        /// ⚠️ Chạy trên ThreadPool thread (Timer.Elapsed)!
        /// </summary>
        private void OnScaleDataChanged(object? sender, DataValueChangedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var newData = e.NewValue;

                // Cập nhật trạng thái kết nối
                ScaleStatus = newData.DriverStatus;

                if (newData.DriverStatus == DriverStatus.Connected)
                {
                    // Lấy giá trị cân (double kg)
                    ScaleValue = Convert.ToDouble(newData.Value ?? 0.0);

                    // Lấy thông tin thêm từ ScaleDriver
                    if (sender is ScaleDriver scaleDriver)
                    {
                        ScaleUnit = scaleDriver.Unit;
                        ScaleStable = scaleDriver.IsStable;
                        ScaleTare = scaleDriver.IsTare;
                    }

                    // Chỉ log khi có giá trị (không log 0 liên tục để tránh spam)
                    // AppendLog(ref _scaleLog, nameof(ScaleLog), $"Cân: {ScaleValue:F3} {ScaleUnit}");
                }
                else if (newData.DriverStatus == DriverStatus.Disconnected)
                {
                    ScaleValue = 0;
                    AppendLog(ref _scaleLog, nameof(ScaleLog), "Scale: Mất kết nối.");
                }
            });
        }

        // ===================================================
        // NGẮT KẾT NỐI TẤT CẢ DRIVERS
        // ===================================================

        /// <summary>
        /// Ngắt đăng ký events và dispose tất cả drivers.
        /// Gọi khi MainWindow đóng hoặc khi muốn tắt drivers.
        /// </summary>
        public void DisposeAllDrivers()
        {
            // Hủy đăng ký events trước (quan trọng, tránh memory leak)
            _barcodeDriver.DataValueChanged -= OnBarcodeDataChanged;
            _rfidDriver.DataValueChanged -= OnRfidDataChanged;
            if (_scaleDriver != null)
                _scaleDriver.DataValueChanged -= OnScaleDataChanged;

            // Dispose từng driver
            _barcodeDriver.Dispose();
            _rfidDriver.Dispose();
            _scaleDriver?.Dispose();
            _scaleDriver = null;

            // Cập nhật UI về trạng thái ban đầu
            BarcodeStatus = DriverStatus.Disconnected;
            RfidStatus = DriverStatus.Disconnected;
            ScaleStatus = DriverStatus.Disconnected;
            IsInitialized = false;
            StatusMessage = "Tất cả drivers đã ngắt kết nối.";
        }

        // ===================================================
        // HELPERS
        // ===================================================

        /// <summary>
        /// Chuyển DriverStatus thành chuỗi hiển thị có biểu tượng màu sắc.
        /// </summary>
        private static string GetStatusText(DriverStatus status) => status switch
        {
            DriverStatus.Connected => "● Đã kết nối",
            DriverStatus.Disconnected => "○ Mất kết nối",
            DriverStatus.Reconnecting => "◌ Đang kết nối lại...",
            _ => "? Không xác định"
        };

        /// <summary>
        /// Thêm một dòng log mới và thông báo WPF cập nhật binding.
        /// Chỉ giữ 50 dòng gần nhất để tránh chiếm quá nhiều bộ nhớ.
        /// </summary>
        private void AppendLog(ref string logField, string propertyName, string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var newLine = $"[{timestamp}] {message}";

            // Giới hạn số dòng log
            var lines = logField.Split('\n').ToList();
            lines.Insert(0, newLine); // Thêm dòng mới lên đầu
            if (lines.Count > 50)
                lines = lines.Take(50).ToList(); // Chỉ giữ 50 dòng

            logField = string.Join('\n', lines);
            OnPropertyChanged(propertyName); // Thông báo WPF cập nhật
        }

        // ===================================================
        // IDISPOSABLE
        // ===================================================

        public void Dispose()
        {
            DisposeAllDrivers();
        }
    }
}
