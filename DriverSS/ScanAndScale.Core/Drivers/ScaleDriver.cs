// ============================================================
// File: Drivers/ScaleDriver.cs
// Mục đích: Driver đọc giá trị cân điện tử qua TCP/IP (Telnet)
//
// Nguyên lý hoạt động:
//   1. Kết nối TCP đến IP:Port của cân
//   2. Timer đọc liên tục theo chu kỳ TimeScanMs
//   3. Mỗi lần đọc: nhận một dòng dữ liệu thô từ cân
//   4. Gọi hàm GetWeight() trong DLL model cân để parse dữ liệu
//   5. Bắn sự kiện DataValueChanged với giá trị đã parse
//
// AUTO-RECONNECT:
//   Khi TCP mất kết nối (Connected=false hoặc exception khi đọc),
//   driver dừng read-timer, chuyển sang Reconnecting và thử kết nối
//   lại TCP mỗi 3 giây. Khi thành công, read-timer được khởi động lại.
//
// QUAN TRỌNG VỀ THREAD:
//   Timer callback và đọc dữ liệu chạy trên ThreadPool thread.
//   Trong WPF: dùng Application.Current.Dispatcher.Invoke(...)
//
// FIX (reconnect freeze):
//   Dùng SemaphoreSlim thay Monitor để tránh SynchronizationLockException.
//   Monitor.Exit() phải gọi trên cùng thread với Monitor.Enter().
//   Sau await, code có thể resume trên thread khác → Exit ném exception
//   trong finally → timer không bao giờ restart → giá trị đóng băng.
//   SemaphoreSlim.Release() an toàn với mọi thread.
//
//   NetworkStream + StreamReader được giữ persistent suốt 1 kết nối
//   (tạo lại khi reconnect) để tránh 2 read chạy song song khi timeout.
// ============================================================

using ScanAndScale.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace ScanAndScale.Core.Drivers
{
    /// <summary>
    /// Driver đọc giá trị cân điện tử qua TCP/IP.
    /// KHÔNG phải Singleton — mỗi cân là một instance riêng biệt.
    /// Hỗ trợ tự động kết nối lại khi mất kết nối TCP.
    /// </summary>
    public class ScaleDriver : IDisposable
    {
        // ===================================================
        // FIELDS
        // ===================================================
        private TcpClient?              _tcpClient;
        private Socket?                 _socket;
        private NetworkStream?          _networkStream;     // Persistent — tạo 1 lần/kết nối
        private StreamReader?           _streamReader;      // Persistent — tạo 1 lần/kết nối
        private Task<string?>?          _pendingReadTask;   // ReadLineAsync đang chờ (dùng lại khi timeout)
        private System.Timers.Timer?    _readTimer;
        private ScaleConfig?            _config;
        private object?                 _scaleModelInstance;
        private MethodInfo?             _getWeightMethod;
        private DataValue               _currentDataValue = new DataValue(DriverStatus.Unknown, null);

        private double  _weightKg;
        private bool    _isStable;
        private bool    _isTare;
        private string  _unit     = "KG";

        // ── FIX: SemaphoreSlim thay Monitor — Release() an toàn mọi thread ──
        private readonly SemaphoreSlim _timerSemaphore = new SemaphoreSlim(1, 1);
        private bool                   _disposed        = false;

        // ── Auto-reconnect ──────────────────────────────────
        private CancellationTokenSource? _reconnectCts;
        private volatile bool _isReconnecting = false;
        private const int ReconnectDelayMs = 3000;
        private const int ConnectTimeoutMs = 5000;

        public string? RawData { get; private set; }

        // ===================================================
        // PROPERTIES
        // ===================================================
        public DataValue CurrentValue => _currentDataValue;
        public bool      IsConnected  => _tcpClient?.Connected == true;
        public bool      IsStable     => _isStable;
        public bool      IsTare       => _isTare;
        public string    Unit         => _unit;

        // ===================================================
        // EVENT
        // ===================================================
        private EventHandler<DataValueChangedEventArgs>? _dataValueChanged;

        /// <summary>
        /// Fired khi giá trị cân hoặc trạng thái thay đổi.
        /// ⚠️ Chạy trên ThreadPool — dispatch về UI thread khi cập nhật UI.
        /// </summary>
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add    => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }

        // ===================================================
        // CONSTRUCTOR
        // ===================================================
        public ScaleDriver() { }

        // ===================================================
        // INITIALIZE
        // ===================================================

        /// <summary>Khởi tạo driver: tải DLL model, kết nối TCP, start timer.</summary>
        public void Initialize(ScaleConfig? config = null)
        {
            _config = config ?? new ScaleConfig();

            if (!_config.Enable)
            {
                LogInfo("ScaleDriver bị vô hiệu hóa (Enable=false).");
                return;
            }

            if (!LoadScaleModel())
            {
                LogInfo($"Không tải được DLL model cân: {_config.ModelName}.dll");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                return;
            }

            _ = ConnectAsync();
        }

        // ===================================================
        // LOAD DLL MODEL CÂN
        // ===================================================
        private bool LoadScaleModel()
        {
            try
            {
                string dllFileName = $"{_config!.ModelName}.dll";

                var assembly = LoadAssemblyFromEmbeddedResource(dllFileName);

                if (assembly != null)
                {
                    LogInfo($"Load model cân từ embedded resource: {dllFileName}");
                }
                else
                {
                    var available = typeof(ScaleDriver).Assembly.GetManifestResourceNames();
                    if (available.Length == 0)
                        LogInfo("[WARN] Không có embedded resource — cần Rebuild Solution.");
                    else
                        LogInfo($"[WARN] Không tìm thấy '{dllFileName}'. Resources: {string.Join(", ", available)}");

                    string dllFullPath = FindDllPath(dllFileName);
                    if (!File.Exists(dllFullPath))
                    {
                        LogInfo($"Không tìm thấy file: {dllFileName}");
                        return false;
                    }
#if NETFRAMEWORK
                    assembly = Assembly.LoadFrom(dllFullPath);
#else
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllFullPath);
#endif
                    LogInfo($"Load model cân từ file: {dllFullPath}");
                }

                string typeName  = $"{_config.ModelName}.ScaleReading";
                var scaleType    = assembly.GetType(typeName);
                if (scaleType == null)
                {
                    LogInfo($"Không tìm thấy class '{typeName}'.");
                    return false;
                }

                _getWeightMethod = scaleType.GetMethod("GetWeight", new Type[]
                {
                    typeof(double?).MakeByRefType(),
                    typeof(bool?).MakeByRefType(),
                    typeof(bool?).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                    typeof(string)
                });

                if (_getWeightMethod == null)
                {
                    LogInfo($"Không tìm thấy method 'GetWeight' trong '{typeName}'.");
                    return false;
                }

                _scaleModelInstance = Activator.CreateInstance(scaleType);
                LogInfo($"Load model cân thành công: {_config.ModelName}");
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "LoadScaleModel");
                return false;
            }
        }

        private static System.Reflection.Assembly? LoadAssemblyFromEmbeddedResource(string dllFileName)
        {
            try
            {
                var coreAssembly = typeof(ScaleDriver).Assembly;
                using var stream = coreAssembly.GetManifestResourceStream(dllFileName);
                if (stream == null) return null;

                var bytes      = new byte[stream.Length];
                var totalRead  = 0;
                while (totalRead < bytes.Length)
                {
                    var read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                return System.Reflection.Assembly.Load(bytes);
            }
            catch { return null; }
        }

        private static string FindDllPath(string dllFileName)
        {
            string? exeDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "");
            if (!string.IsNullOrEmpty(exeDir))
            {
                string path1 = Path.Combine(exeDir, dllFileName);
                if (File.Exists(path1)) return path1;
            }
            return Path.GetFullPath(dllFileName);
        }

        // ===================================================
        // TCP CONNECT
        // ===================================================

        /// <summary>Kết nối TCP lần đầu, sau đó start read-timer.</summary>
        private async Task ConnectAsync()
        {
            try
            {
                LogInfo($"Đang kết nối TCP đến {_config!.IP}:{_config.Port}...");
                _tcpClient = new TcpClient();

                var connectTask = _tcpClient.ConnectAsync(_config.IP, _config.Port);
                var timeoutTask = Task.Delay(ConnectTimeoutMs);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    LogInfo($"Kết nối TCP timeout ({ConnectTimeoutMs}ms). IP: {_config.IP}");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    return;
                }

                await connectTask;
                _socket = _tcpClient.Client;

                // Tạo persistent stream/reader cho kết nối này
                CreateStreamReader();

                LogInfo($"Kết nối TCP thành công: {_config.IP}:{_config.Port}");
                StartReadTimer();
            }
            catch (Exception ex)
            {
                LogError(ex, "ConnectAsync");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
            }
        }

        // ===================================================
        // READ TIMER
        // ===================================================
        private void StartReadTimer()
        {
            _readTimer          = new System.Timers.Timer(_config!.TimeScanMs);
            _readTimer.Elapsed += OnReadTimerElapsed;
            _readTimer.AutoReset = false;   // Manual restart sau mỗi lần đọc
            _readTimer.Start();
            LogInfo($"Read timer start (interval={_config.TimeScanMs}ms).");
        }

        private async void OnReadTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // ── FIX: SemaphoreSlim.Wait(0) thay Monitor.TryEnter ──────────────
            // Monitor.Exit() ném SynchronizationLockException nếu gọi từ thread khác
            // với thread đã TryEnter — điều này xảy ra thường xuyên sau async/await
            // vì ThreadPool không đảm bảo resume trên cùng thread.
            // SemaphoreSlim.Release() an toàn với bất kỳ thread nào.
            if (!_timerSemaphore.Wait(0)) return;

            try
            {
                // Kiểm tra kết nối TCP
                if (_tcpClient == null || !_tcpClient.Connected)
                {
                    LogInfo("TCP mất kết nối — khởi động auto-reconnect.");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    StartReconnectLoop();   // Reconnect loop sẽ restart timer sau khi thành công
                    return;                 // Không restart timer ở đây
                }

                await ReadScaleDataAsync();
            }
            catch (Exception ex)
            {
                LogError(ex, "OnReadTimerElapsed");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                StartReconnectLoop();
                return;
            }
            finally
            {
                // Release() luôn an toàn bất kể đang ở thread nào
                _timerSemaphore.Release();
            }

            // Restart timer chỉ khi không đang reconnect
            if (!_disposed && !_isReconnecting && _readTimer != null)
                _readTimer.Start();
        }

        // ===================================================
        // STREAM READER HELPERS
        // ===================================================

        /// <summary>
        /// Tạo NetworkStream + StreamReader persistent từ socket hiện tại.
        /// Dispose stream/reader cũ trước khi tạo mới.
        /// </summary>
        private void CreateStreamReader()
        {
            DisposeStreamReader();
            if (_socket == null) return;
            // ownsSocket: false — socket do _tcpClient quản lý, không đóng khi stream dispose
            _networkStream = new NetworkStream(_socket, ownsSocket: false);
            _streamReader  = new StreamReader(_networkStream);
            _pendingReadTask = null;
            LogInfo("NetworkStream + StreamReader đã khởi tạo.");
        }

        /// <summary>Dispose stream/reader và xóa pending task.</summary>
        private void DisposeStreamReader()
        {
            _pendingReadTask = null;
            try { _streamReader?.Dispose(); }  catch { /* ignored */ }
            try { _networkStream?.Dispose(); } catch { /* ignored */ }
            _streamReader  = null;
            _networkStream = null;
        }

        // ===================================================
        // READ & PARSE
        // ===================================================
        private async Task ReadScaleDataAsync()
        {
            try
            {
                if (_streamReader == null)
                    throw new InvalidOperationException("StreamReader chưa được khởi tạo.");

                // ── FIX: Tái sử dụng pending task nếu vẫn còn chạy (timeout tick trước) ──
                // Gọi ReadLineAsync() hai lần đồng thời trên cùng StreamReader là lỗi.
                // Nếu tick trước bị timeout mà chưa đọc xong, dùng lại task đó.
                if (_pendingReadTask == null || _pendingReadTask.IsCompleted)
                    _pendingReadTask = _streamReader.ReadLineAsync();

                var timeoutTask = Task.Delay(3000);

                if (await Task.WhenAny(_pendingReadTask, timeoutTask) == timeoutTask)
                {
                    LogInfo("Timeout đọc dữ liệu từ cân — chờ tick tiếp theo.");
                    // _pendingReadTask còn chạy, sẽ được tái sử dụng ở tick kế
                    return;
                }

                RawData = await _pendingReadTask;
                _pendingReadTask = null;    // Task đã hoàn thành, reset

                if (string.IsNullOrEmpty(RawData)) return;

                ParseScaleData(RawData);
                SetDataValue(new DataValue(DriverStatus.Connected, _weightKg));
            }
            catch (Exception ex)
            {
                // Lỗi network → báo Disconnected và khởi động reconnect
                _pendingReadTask = null;
                LogError(ex, "ReadScaleDataAsync");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                StartReconnectLoop();
            }
        }

        private void ParseScaleData(string rawData)
        {
            try
            {
                if (_getWeightMethod == null || _scaleModelInstance == null) return;

                object?[] parameters = { null, null, null, "", rawData };
                _getWeightMethod.Invoke(_scaleModelInstance, parameters);

                _weightKg = (double?)parameters[0] ?? 0.0;
                _isStable = (bool?)parameters[1]   ?? false;
                _isTare   = (bool?)parameters[2]   ?? false;
                _unit     = (string?)parameters[3]  ?? "Kg";
            }
            catch (Exception ex)
            {
                LogError(ex, "ParseScaleData");
            }
        }

        // ===================================================
        // AUTO-RECONNECT LOOP
        // ===================================================

        /// <summary>
        /// Dừng read-timer, chuyển sang Reconnecting và thử kết nối lại TCP
        /// mỗi <see cref="ReconnectDelayMs"/> ms cho đến khi thành công hoặc dispose.
        /// Sau khi kết nối lại thành công, read-timer được khởi động lại.
        /// </summary>
        private void StartReconnectLoop()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            // Dừng read-timer trước khi bắt đầu reconnect
            _readTimer?.Stop();

            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            Task.Run(async () =>
            {
                LogInfo($"[Reconnect] Bắt đầu auto-reconnect Scale (delay={ReconnectDelayMs}ms)...");
                SetDataValue(new DataValue(DriverStatus.Reconnecting, 0.0));

                while (!token.IsCancellationRequested && !_disposed)
                {
                    try { await Task.Delay(ReconnectDelayMs, token); }
                    catch (OperationCanceledException) { break; }

                    if (token.IsCancellationRequested || _disposed) break;

                    LogInfo("[Reconnect] Đang thử kết nối lại TCP cân...");
                    bool ok = await TryReconnectTcpAsync();

                    if (ok)
                    {
                        LogInfo("[Reconnect] Kết nối lại Scale thành công — restart read-timer.");
                        _isReconnecting = false;
                        StartReadTimer();   // Tiếp tục đọc dữ liệu
                        return;
                    }

                    LogInfo($"[Reconnect] Thất bại. Thử lại sau {ReconnectDelayMs}ms...");
                    // Giữ nguyên trạng thái Reconnecting
                }

                _isReconnecting = false;
                LogInfo("[Reconnect] Vòng lặp auto-reconnect Scale kết thúc.");
            }, token);
        }

        /// <summary>Đóng kết nối cũ và thử kết nối TCP mới.</summary>
        private async Task<bool> TryReconnectTcpAsync()
        {
            try
            {
                // Đóng stream/reader và socket/client cũ
                DisposeStreamReader();
                try { _socket?.Close();   } catch { /* ignored */ }
                try { _tcpClient?.Close(); _tcpClient?.Dispose(); } catch { /* ignored */ }
                _socket    = null;
                _tcpClient = null;

                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_config!.IP, _config.Port);
                var timeoutTask = Task.Delay(ConnectTimeoutMs);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    LogInfo($"[Reconnect] TCP timeout ({ConnectTimeoutMs}ms). IP: {_config.IP}");
                    return false;
                }

                await connectTask;
                _socket = _tcpClient.Client;

                // Tạo persistent stream/reader cho kết nối mới
                CreateStreamReader();

                SetDataValue(new DataValue(DriverStatus.Connected, 0.0));
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "TryReconnectTcpAsync");
                return false;
            }
        }

        // ===================================================
        // DISCONNECT
        // ===================================================

        /// <summary>Dừng timer, hủy reconnect loop và đóng kết nối TCP.</summary>
        public void Disconnect()
        {
            try
            {
                // Dừng reconnect loop trước
                _reconnectCts?.Cancel();
                _isReconnecting = false;

                _readTimer?.Stop();
                _readTimer?.Dispose();
                _readTimer = null;

                // Dispose stream/reader trước khi đóng socket
                DisposeStreamReader();

                _socket?.Close();
                _socket = null;

                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;

                SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                LogInfo("ScaleDriver đã ngắt kết nối.");
            }
            catch (Exception ex)
            {
                LogError(ex, "Disconnect");
            }
        }

        // ===================================================
        // SET DATA VALUE
        // ===================================================
        private void SetDataValue(DataValue newValue)
        {
            if (_currentDataValue.Equals(newValue)) return;
            var oldValue      = _currentDataValue;
            _currentDataValue = newValue;
            _dataValueChanged?.Invoke(this, new DataValueChangedEventArgs(oldValue, newValue));
        }

        // ===================================================
        // DISPOSE
        // ===================================================
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;

            Disconnect();

            _timerSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }

        // ===================================================
        // LOG HELPERS
        // ===================================================
        private static void LogInfo(string msg)                => Debug.WriteLine($"[ScaleDriver] {msg}");
        private static void LogError(Exception ex, string ctx) => Debug.WriteLine($"[ScaleDriver][ERROR] {ctx}: {ex.Message}");
    }
}
