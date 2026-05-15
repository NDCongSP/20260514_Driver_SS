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
// AUTO-RECONNECT:
//   Khi SerialPort mất kết nối (exception trong DataReceived),
//   driver tự động chuyển sang Reconnecting và thử lại mỗi 3 giây.
//
// QUAN TRỌNG VỀ THREAD:
//   SerialPort.DataReceived chạy trên ThreadPool thread.
//   Trong WPF: dùng Application.Current.Dispatcher.Invoke(...)
//   Trong WinForms: dùng control.Invoke(...)
// ============================================================

using ScanAndScale.Core.Models;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ScanAndScale.Core.Drivers
{
    /// <summary>
    /// Driver đọc thẻ RFID qua cổng Serial COM.
    /// Singleton — toàn ứng dụng dùng chung một instance.
    /// Hỗ trợ tự động kết nối lại khi mất kết nối.
    /// </summary>
    public sealed class RfidDriver : IDisposable
    {
        // ===================================================
        // SINGLETON
        // ===================================================
        private static RfidDriver? _instance;
        private static readonly object _instanceLock = new object();

        public static RfidDriver Instance
        {
            get
            {
                if (_instance == null)
                    lock (_instanceLock)
                        if (_instance == null)
                            _instance = new RfidDriver();
                return _instance;
            }
        }

        // ===================================================
        // FIELDS
        // ===================================================
        private SerialPort?  _serialPort;
        private RfidConfig?  _config;
        private Regex?       _dataRegex;
        private DataValue    _currentDataValue = new DataValue(DriverStatus.Unknown, null);
        private bool         _disposed         = false;

        private readonly object _portLock = new object();

        // ── Auto-reconnect ──────────────────────────────────
        private CancellationTokenSource? _reconnectCts;
        private volatile bool _isReconnecting = false;
        private const int ReconnectDelayMs = 3000;

        // ===================================================
        // PROPERTIES
        // ===================================================
        public string?    CurrentComPort => _serialPort?.PortName;
        public bool       IsConnected    => _serialPort?.IsOpen == true;
        public DataValue  CurrentValue   => _currentDataValue;

        // ===================================================
        // EVENT
        // ===================================================
        private EventHandler<DataValueChangedEventArgs>? _dataValueChanged;

        /// <summary>
        /// Fired khi quét được thẻ hoặc trạng thái kết nối thay đổi.
        /// ⚠️ Chạy trên ThreadPool — dispatch về UI thread khi cập nhật UI.
        /// </summary>
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add    => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }

        // ===================================================
        // CONSTRUCTOR (private – Singleton)
        // ===================================================
        private RfidDriver() { }

        // ===================================================
        // INITIALIZE
        // ===================================================

        /// <summary>Khởi tạo driver và mở cổng COM.</summary>
        public bool Initialize(RfidConfig? config = null, bool isReconnect = false)
        {
            _config = config ?? new RfidConfig();

            if (!_config.Enable)
            {
                LogInfo("RfidDriver bị vô hiệu hóa (Enable=false).");
                return false;
            }

            try
            {
                _dataRegex = new Regex(_config.DataPattern, RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                LogError(ex, "Compile Regex thất bại");
                _dataRegex = new Regex(@"\b0*(\d{5})\b", RegexOptions.Compiled);
            }

            return OpenSerialPort();
        }

        // ===================================================
        // OPEN PORT
        // ===================================================
        private bool OpenSerialPort()
        {
            lock (_portLock)
            {
                try
                {
                    if (_serialPort?.IsOpen == true)
                    {
                        LogInfo("SerialPort đã mở sẵn.");
                        return true;
                    }

                    string? comPort;
                    if (_config!.AutoFindCom)
                    {
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

                    _serialPort = new SerialPort(comPort, _config.BaudRate, Parity.None, 8, StopBits.One);
                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.Open();

                    LogInfo($"Kết nối RFID thành công tại {comPort} ({_config.BaudRate} baud).");
                    SetDataValue(new DataValue(DriverStatus.Connected, null));
                    return true;
                }
                catch (Exception ex)
                {
                    LogError(ex, "OpenSerialPort");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                    return false;
                }
            }
        }

        // ===================================================
        // DATA RECEIVED
        // ===================================================
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string rawData = _serialPort!.ReadLine();
                LogInfo($"Dữ liệu thô: '{rawData}'");

                if (string.IsNullOrEmpty(rawData)) return;

                string rfidCode;
                var match = _dataRegex!.Match(rawData);
                if (match.Success)
                {
                    rfidCode = match.Groups[1].Value;
                    LogInfo($"Mã RFID hợp lệ: {rfidCode}");
                }
                else
                {
                    rfidCode = "Scan again";
                    LogInfo("Dữ liệu RFID không đúng định dạng.");
                }

                SetDataValue(new DataValue(DriverStatus.Connected, rfidCode));
            }
            catch (TimeoutException)
            {
                // ReadLine timeout — bỏ qua, không nghiêm trọng
                LogInfo("SerialPort ReadLine timeout.");
            }
            catch (Exception ex)
            {
                // Lỗi thực (port bị rút, ngắt điện, v.v.) → khởi động auto-reconnect
                LogError(ex, "SerialPort_DataReceived");
                SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                StartReconnectLoop();
            }
        }

        // ===================================================
        // AUTO-FIND COM PORT
        // ===================================================
        public static string? AutoFindComPort(string caption = "Pongee", string manufacturer = "Prolific")
        {
            try
            {
                using var entity = new ManagementClass("Win32_PnPEntity");
                const string COM_PORT_GUID = "{4D36E978-E325-11CE-BFC1-08002BE10318}";
                const string REG_ROOT      = "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\";

                foreach (ManagementObject instance in entity.GetInstances())
                {
                    var classGuid = instance.GetPropertyValue("ClassGuid")?.ToString()?.ToUpper();
                    if (classGuid != COM_PORT_GUID) continue;

                    var deviceCaption = instance.GetPropertyValue("Caption")?.ToString()       ?? "";
                    var deviceMfg     = instance.GetPropertyValue("Manufacturer")?.ToString()  ?? "";
                    var deviceId      = instance.GetPropertyValue("PnpDeviceID")?.ToString()   ?? "";

                    var regPath  = $"{REG_ROOT}Enum\\{deviceId}\\Device Parameters";
                    var portName = Registry.GetValue(regPath, "PortName", "")?.ToString();

                    var cleanCaption = deviceCaption;
                    var comIdx = cleanCaption.IndexOf(" (COM", StringComparison.Ordinal);
                    if (comIdx > 0) cleanCaption = cleanCaption.Substring(0, comIdx);

                    LogInfo($"COM device: {portName} | {cleanCaption} | {deviceMfg}");

                    if (cleanCaption == caption && deviceMfg == manufacturer)
                        return portName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RfidDriver] AutoFindComPort lỗi: {ex.Message}");
            }
            return null;
        }

        // ===================================================
        // AUTO-RECONNECT LOOP
        // ===================================================

        /// <summary>
        /// Khởi động vòng lặp tự động kết nối lại ở background thread.
        /// Chỉ chạy một vòng lặp tại một thời điểm.
        /// Mỗi lần thử cách nhau <see cref="ReconnectDelayMs"/> ms.
        /// </summary>
        private void StartReconnectLoop()
        {
            if (_isReconnecting) return;   // Đã có vòng lặp đang chạy
            _isReconnecting = true;

            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            Task.Run(async () =>
            {
                LogInfo($"[Reconnect] Bắt đầu auto-reconnect RFID (delay={ReconnectDelayMs}ms)...");
                SetDataValue(new DataValue(DriverStatus.Reconnecting, null));

                while (!token.IsCancellationRequested && !_disposed)
                {
                    try { await Task.Delay(ReconnectDelayMs, token); }
                    catch (OperationCanceledException) { break; }

                    if (token.IsCancellationRequested || _disposed) break;

                    LogInfo("[Reconnect] Đang thử kết nối lại RFID...");
                    ClosePort();

                    bool ok = OpenSerialPort();
                    if (ok)
                    {
                        // OpenSerialPort đã set Connected
                        LogInfo("[Reconnect] Kết nối lại RFID thành công.");
                        _isReconnecting = false;
                        return;
                    }

                    // OpenSerialPort đã set Disconnected → reset về Reconnecting
                    // để UI tiếp tục thể hiện đang cố gắng kết nối
                    LogInfo($"[Reconnect] Thất bại. Thử lại sau {ReconnectDelayMs}ms...");
                    SetDataValue(new DataValue(DriverStatus.Reconnecting, null));
                }

                _isReconnecting = false;
                LogInfo("[Reconnect] Vòng lặp auto-reconnect RFID kết thúc.");
            }, token);
        }

        // ===================================================
        // MANUAL RECONNECT
        // ===================================================

        /// <summary>
        /// Kết nối lại thủ công (từ UI).
        /// Hủy vòng lặp tự động hiện tại rồi khởi động lại.
        /// </summary>
        public void Reconnect()
        {
            LogInfo("Manual reconnect được yêu cầu.");
            _reconnectCts?.Cancel();
            _isReconnecting = false;
            ClosePort();
            StartReconnectLoop();
        }

        // ===================================================
        // CLOSE PORT
        // ===================================================
        private void ClosePort()
        {
            lock (_portLock)
            {
                if (_serialPort == null) return;
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
                    LogError(ex, "ClosePort");
                }
            }
        }

        // ===================================================
        // SET DATA VALUE
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

        /// <summary>Đóng port, hủy reconnect loop và giải phóng tài nguyên.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts   = null;
            _isReconnecting = false;

            ClosePort();
            SetDataValue(new DataValue(DriverStatus.Disconnected, null));
            _instance = null;
            LogInfo("RfidDriver đã được dispose.");
        }

        private static void LogInfo(string msg)             => Debug.WriteLine($"[RfidDriver] {msg}");
        private static void LogError(Exception ex, string ctx) => Helpers.DriverHelper.LogError(ex, ctx);
    }
}
