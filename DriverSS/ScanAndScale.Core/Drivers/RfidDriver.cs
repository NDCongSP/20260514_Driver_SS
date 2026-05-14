// ============================================================
// File: Drivers/RfidDriver.cs
// Mục đích: Driver đọc thẻ RFID qua cổng Serial (COM Port)
//
// Nguyên lý hoạt động:
//   1. Mở SerialPort (tự tìm COM hoặc dùng COM chỉ định)
//   2. Lắng nghe sự kiện DataReceived từ SerialPort
//   3. Parse dữ liệu bằng Regex để lấy mã số thẻ
//   4. Bắn sự kiện DataValueChanged cho subscriber
//
// QUAN TRỌNG VỀ THREAD:
//   SerialPort.DataReceived chạy trên ThreadPool thread.
//   Trong WPF: dùng Application.Current.Dispatcher.Invoke(...)
//   Trong WinForms: dùng control.Invoke(...)
// ============================================================

using ScanAndScale.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace ScanAndScale.Core.Drivers
{
    /// <summary>
    /// Driver đọc thẻ RFID qua cổng Serial COM.
    /// <para>
    /// Singleton pattern — toàn ứng dụng dùng chung một instance.
    /// </para>
    /// <para>
    /// Cách dùng:
    /// <code>
    ///   var driver = RfidDriver.Instance;
    ///   driver.Initialize(new RfidConfig {
    ///       Enable = true,
    ///       AutoFindCom = true,
    ///       DeviceCaption = "Pongee",
    ///       DeviceManufacturer = "Prolific"
    ///   });
    ///   driver.DataValueChanged += (s, e) => {
    ///     Console.WriteLine("RFID: " + e.NewValue.Value);
    ///   };
    ///   // Khi thoát:
    ///   driver.Dispose();
    /// </code>
    /// </para>
    /// </summary>
    public sealed class RfidDriver : IDisposable
    {
        // ===================================================
        // SINGLETON PATTERN
        // ===================================================
        private static RfidDriver? _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>Instance duy nhất của RfidDriver.</summary>
        public static RfidDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                            _instance = new RfidDriver();
                    }
                }
                return _instance;
            }
        }

        // ===================================================
        // FIELDS — Biến nội bộ
        // ===================================================

        // SerialPort object — kết nối đến thiết bị RFID qua COM
        private SerialPort? _serialPort;

        // Config hiện tại
        private RfidConfig? _config;

        // Regex để parse mã thẻ từ chuỗi nhận được
        private Regex? _dataRegex;

        // Giá trị RFID gần nhất
        private DataValue _currentDataValue = new DataValue(DriverStatus.Unknown, null);

        // Đã dispose chưa
        private bool _disposed = false;

        // Lock object để tránh race condition khi mở/đóng port
        private readonly object _portLock = new object();

        // ===================================================
        // PROPERTIES
        // ===================================================

        /// <summary>Tên cổng COM đang được dùng.</summary>
        public string? CurrentComPort => _serialPort?.PortName;

        /// <summary>SerialPort đang mở không.</summary>
        public bool IsConnected => _serialPort?.IsOpen == true;

        /// <summary>Giá trị RFID hiện tại.</summary>
        public DataValue CurrentValue => _currentDataValue;

        // ===================================================
        // EVENTS
        // ===================================================

        private EventHandler<DataValueChangedEventArgs>? _dataValueChanged;

        /// <summary>
        /// Sự kiện kích hoạt khi quét được thẻ RFID mới, hoặc trạng thái thay đổi.
        /// <para>⚠️ Chạy trên ThreadPool thread — cần dispatch về UI thread khi cập nhật UI.</para>
        /// </summary>
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }

        // ===================================================
        // CONSTRUCTOR — Private (Singleton)
        // ===================================================
        private RfidDriver() { }

        // ===================================================
        // KHỞI TẠO VÀ KẾT NỐI
        // ===================================================

        /// <summary>
        /// Khởi tạo và mở kết nối SerialPort cho RFID Reader.
        /// </summary>
        /// <param name="config">Cấu hình RFID. Null = dùng mặc định.</param>
        /// <param name="isReconnect">true nếu đây là kết nối lại (không tăng NumSlave).</param>
        /// <returns>true nếu mở cổng COM thành công.</returns>
        public bool Initialize(RfidConfig? config = null, bool isReconnect = false)
        {
            _config = config ?? new RfidConfig();

            // Kiểm tra enable
            if (!_config.Enable)
            {
                LogInfo("RfidDriver bị vô hiệu hóa (Enable=false).");
                return false;
            }

            // Compile regex từ pattern trong config
            try
            {
                _dataRegex = new Regex(_config.DataPattern, RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                LogError(ex, "RfidDriver: Compile Regex thất bại");
                _dataRegex = new Regex(@"\b0*(\d{5})\b", RegexOptions.Compiled); // Fallback pattern
            }

            return OpenSerialPort();
        }

        /// <summary>
        /// Mở cổng COM và bắt đầu nhận dữ liệu.
        /// </summary>
        private bool OpenSerialPort()
        {
            lock (_portLock)
            {
                try
                {
                    // Nếu đã mở rồi thì không cần mở lại
                    if (_serialPort?.IsOpen == true)
                    {
                        LogInfo("SerialPort đã mở sẵn.");
                        return true;
                    }

                    // Xác định cổng COM cần dùng
                    string? comPort;

                    if (_config!.AutoFindCom)
                    {
                        // Tự động tìm cổng COM theo Caption và Manufacturer
                        comPort = AutoFindComPort(_config.DeviceCaption, _config.DeviceManufacturer);
                        if (string.IsNullOrEmpty(comPort))
                        {
                            LogInfo($"Không tìm thấy thiết bị RFID " +
                                    $"(Caption='{_config.DeviceCaption}', Mfg='{_config.DeviceManufacturer}').");
                            SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                            return false;
                        }
                        LogInfo($"Tìm thấy thiết bị RFID tại: {comPort}");
                    }
                    else
                    {
                        comPort = _config.ComPort;
                    }

                    // Tạo và cấu hình SerialPort
                    _serialPort = new SerialPort(
                        comPort,
                        _config.BaudRate,
                        Parity.None,
                        8,
                        StopBits.One
                    );

                    // Đăng ký sự kiện nhận dữ liệu
                    _serialPort.DataReceived += SerialPort_DataReceived;

                    // Mở cổng
                    _serialPort.Open();

                    LogInfo($"Kết nối RFID thành công tại {comPort} ({_config.BaudRate} baud).");
                    SetDataValue(new DataValue(DriverStatus.Connected, null));
                    return true;
                }
                catch (Exception ex)
                {
                    LogError(ex, "RfidDriver.OpenSerialPort");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                    return false;
                }
            }
        }

        // ===================================================
        // XỬ LÝ DỮ LIỆU NHẬN ĐƯỢC TỪ SERIAL PORT
        // ===================================================

        /// <summary>
        /// Callback được gọi bởi SerialPort khi có dữ liệu đến.
        /// ⚠️ Chạy trên ThreadPool thread!
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // Bước 1: Đọc một dòng dữ liệu từ SerialPort
                string rawData = _serialPort!.ReadLine();

                LogInfo($"Dữ liệu RFID thô nhận được: '{rawData}'");

                if (string.IsNullOrEmpty(rawData))
                    return;

                // Bước 2: Parse dữ liệu bằng Regex để lấy mã số thẻ
                //   Input mẫu: "0000008991\r" (có thể có ký tự control)
                //   Output: "08991" (5 chữ số, loại bỏ leading zeros)
                string rfidCode;
                var match = _dataRegex!.Match(rawData);

                if (match.Success)
                {
                    rfidCode = match.Groups[1].Value;
                    LogInfo($"Mã RFID hợp lệ: {rfidCode}");
                }
                else
                {
                    // Không match pattern — báo cần quét lại
                    rfidCode = "Scan again";
                    LogInfo("Dữ liệu RFID không đúng định dạng.");
                }

                // Bước 3: Cập nhật giá trị và bắn sự kiện
                SetDataValue(new DataValue(DriverStatus.Connected, rfidCode));
            }
            catch (TimeoutException)
            {
                // Timeout đọc dữ liệu — bỏ qua, không phải lỗi nghiêm trọng
                LogInfo("SerialPort ReadLine timeout.");
            }
            catch (Exception ex)
            {
                LogError(ex, "SerialPort_DataReceived");
                SetDataValue(new DataValue(DriverStatus.Disconnected, null));
            }
        }

        // ===================================================
        // TỰ ĐỘNG TÌM CỔNG COM
        // ===================================================

        /// <summary>
        /// Tự động tìm cổng COM của thiết bị RFID dựa vào tên và nhà sản xuất.
        /// Duyệt tất cả thiết bị COM trong Device Manager qua WMI.
        /// </summary>
        /// <param name="caption">Caption (tên hiển thị) trong Device Manager, ví dụ: "Pongee".</param>
        /// <param name="manufacturer">Tên nhà sản xuất, ví dụ: "Prolific".</param>
        /// <returns>Tên cổng COM (ví dụ "COM3"), hoặc null nếu không tìm thấy.</returns>
        public static string? AutoFindComPort(string caption = "Pongee", string manufacturer = "Prolific")
        {
            try
            {
                // Dùng WMI để duyệt tất cả thiết bị Plug-and-Play
                using var entity = new ManagementClass("Win32_PnPEntity");

                // ClassGuid của thiết bị COM port trong Windows
                const string COM_PORT_GUID = "{4D36E978-E325-11CE-BFC1-08002BE10318}";
                const string REG_ROOT = "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\";

                foreach (ManagementObject instance in entity.GetInstances())
                {
                    // Bỏ qua thiết bị không phải COM port
                    var classGuid = instance.GetPropertyValue("ClassGuid")?.ToString()?.ToUpper();
                    if (classGuid != COM_PORT_GUID) continue;

                    // Lấy thông tin thiết bị
                    var deviceCaption = instance.GetPropertyValue("Caption")?.ToString() ?? "";
                    var deviceMfg = instance.GetPropertyValue("Manufacturer")?.ToString() ?? "";
                    var deviceId = instance.GetPropertyValue("PnpDeviceID")?.ToString() ?? "";

                    // Lấy tên cổng COM từ registry
                    var regPath = $"{REG_ROOT}Enum\\{deviceId}\\Device Parameters";
                    var portName = Registry.GetValue(regPath, "PortName", "")?.ToString();

                    // Bỏ phần "(COMx)" trong caption để so sánh thuần túy
                    var cleanCaption = deviceCaption;
                    var comIdx = cleanCaption.IndexOf(" (COM", StringComparison.Ordinal);
                    if (comIdx > 0) cleanCaption = cleanCaption.Substring(0, comIdx);

                    LogInfo($"Tìm thấy COM device: {portName} | Caption: {cleanCaption} | Mfg: {deviceMfg}");

                    // Kiểm tra khớp với tiêu chí tìm kiếm
                    if (cleanCaption == caption && deviceMfg == manufacturer)
                    {
                        return portName;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RfidDriver] AutoFindComPort lỗi: {ex.Message}");
            }

            return null;
        }

        // ===================================================
        // KẾT NỐI LẠI (Reconnect)
        // ===================================================

        /// <summary>
        /// Thử kết nối lại với thiết bị RFID (đóng port cũ, mở port mới).
        /// </summary>
        public void Reconnect()
        {
            LogInfo("Đang kết nối lại...");
            ClosePort();
            Thread.Sleep(500); // Chờ port sẵn sàng
            Initialize(_config, isReconnect: true);
        }

        // ===================================================
        // ĐÓNG KẾT NỐI
        // ===================================================

        /// <summary>
        /// Đóng cổng COM và giải phóng SerialPort.
        /// </summary>
        private void ClosePort()
        {
            lock (_portLock)
            {
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.DataReceived -= SerialPort_DataReceived;
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                        _serialPort = null;

                        LogInfo("Đóng cổng COM thành công.");
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "RfidDriver.ClosePort");
                    }
                }
            }
        }

        // ===================================================
        // CẬP NHẬT GIÁ TRỊ VÀ BẮN EVENT
        // ===================================================

        private void SetDataValue(DataValue newValue)
        {
            var oldValue = _currentDataValue;
            _currentDataValue = newValue;
            _dataValueChanged?.Invoke(this, new DataValueChangedEventArgs(oldValue, newValue));
        }

        // ===================================================
        // DISPOSE
        // ===================================================

        /// <summary>
        /// Đóng SerialPort và giải phóng tài nguyên.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ClosePort();
            SetDataValue(new DataValue(DriverStatus.Disconnected, null));
            _instance = null;
            LogInfo("RfidDriver đã được dispose.");
        }

        private static void LogInfo(string msg) => Debug.WriteLine($"[RfidDriver] {msg}");
        private static void LogError(Exception ex, string ctx) => Helpers.DriverHelper.LogError(ex, ctx);
    }
}
