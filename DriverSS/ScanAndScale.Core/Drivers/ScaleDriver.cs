// ============================================================
// File: Drivers/ScaleDriver.cs
// Mục đích: Driver đọc giá trị cân điện tử qua TCP/IP (Telnet) HOẶC
//           trực tiếp qua cổng COM (RS232/USB-to-Serial) — chọn bằng
//           ScaleConfig.ConnectionType.
//
// Nguyên lý hoạt động:
//   1. Kết nối TCP đến IP:Port (ConnectionType=Tcp) hoặc mở cổng COM
//      (ConnectionType=Com) của cân.
//   2. Timer đọc liên tục theo chu kỳ TimeScanMs — CHUNG cho cả 2 kiểu
//      kết nối, chỉ khác nguồn đọc (NetworkStream vs SerialPort).
//   3. Mỗi lần đọc: nhận một dòng dữ liệu thô từ cân.
//   4. Gọi hàm GetWeight() trong DLL model cân để parse dữ liệu —
//      KHÔNG phụ thuộc kiểu kết nối, model cân không cần biết TCP hay COM.
//   5. Bắn sự kiện DataValueChanged với giá trị đã parse.
//
// AUTO-RECONNECT:
//   Khi mất kết nối (TCP: Connected=false; COM: IsOpen=false, hoặc
//   exception khi đọc ở cả 2 kiểu), driver dừng read-timer, chuyển sang
//   Reconnecting và thử kết nối lại mỗi 3 giây (đúng kiểu kết nối đang
//   dùng). Khi thành công, read-timer được khởi động lại.
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
//   NetworkStream + StreamReader (nhánh TCP) / SerialPort.BaseStream +
//   StreamReader (nhánh COM) được giữ persistent suốt 1 kết nối (tạo
//   lại khi reconnect) để tránh 2 read chạy song song khi timeout.
// ============================================================

using ScanAndScale.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace ScanAndScale.Core.Drivers
{
    /// <summary>
    /// Driver đọc giá trị cân điện tử qua TCP/IP hoặc trực tiếp qua cổng COM
    /// (chọn bằng <see cref="ScaleConfig.ConnectionType"/>).
    /// KHÔNG phải Singleton — mỗi cân là một instance riêng biệt.
    /// Hỗ trợ tự động kết nối lại khi mất kết nối.
    /// </summary>
    public class ScaleDriver : IDisposable
    {
        // ===================================================
        // FIELDS
        // ===================================================
        // Nhánh TCP (ConnectionType = Tcp)
        private TcpClient?              _tcpClient;
        private Socket?                 _socket;
        // Nhánh COM (ConnectionType = Com)
        private SerialPort?             _serialPort;

        private System.Timers.Timer?    _readTimer;
        private ScaleConfig?            _config;
        private object?                 _scaleModelInstance;
        private MethodInfo?             _getWeightMethod;
        // Field debug TUY CHON "LastStabilityInfo" (vd. Scale_Shimadzu_TX4202L) - doc
        // best-effort qua reflection de xem so lieu thuc te driver dung tinh Stable,
        // KHONG bat buoc phai co - Scale_* khac khong co field nay van hoat dong binh
        // thuong (FieldInfo = null => bo qua, khong loi).
        private FieldInfo?              _stabilityDebugField;
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

        /// <summary>
        /// Đã kết nối chưa — kiểm tra đúng field theo <see cref="ScaleConfig.ConnectionType"/>
        /// đang cấu hình (TCP: TcpClient.Connected; COM: SerialPort.IsOpen).
        /// </summary>
        public bool IsConnected =>
            _config?.ConnectionType == ScaleConnectionType.Com
                ? _serialPort?.IsOpen == true
                : _tcpClient?.Connected == true;

        public bool      IsStable     => _isStable;
        public bool      IsTare       => _isTare;
        public string    Unit         => _unit;
        /// <summary>Thông tin debug (tuỳ chọn) về cách driver model cân tính Stable — rỗng nếu model không hỗ trợ.</summary>
        public string    StabilityDebugInfo { get; private set; } = "";

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

                // Optional: field debug "LastStabilityInfo" (static string) - chi vai model
                // (vd. Shimadzu TX4202L) co field nay de tu suy luan Stable o phia phan mem.
                _stabilityDebugField = scaleType.GetField("LastStabilityInfo",
                    BindingFlags.Public | BindingFlags.Static);

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
        // CONNECT (dispatcher theo ConnectionType)
        // ===================================================

        /// <summary>Kết nối lần đầu (TCP hoặc COM tuỳ ConnectionType), sau đó start read-timer.</summary>
        private Task ConnectAsync() =>
            _config!.ConnectionType == ScaleConnectionType.Com
                ? ConnectSerialAsync()
                : ConnectTcpAsync();

        // ===================================================
        // TCP CONNECT
        // ===================================================

        /// <summary>Kết nối TCP lần đầu, sau đó start read-timer.</summary>
        private async Task ConnectTcpAsync()
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

                LogInfo($"Kết nối TCP thành công: {_config.IP}:{_config.Port}");
                StartReadTimer();
            }
            catch (Exception ex)
            {
                LogError(ex, "ConnectTcpAsync");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
            }
        }

        // ===================================================
        // COM CONNECT
        // ===================================================

        /// <summary>Mở cổng COM lần đầu, sau đó start read-timer.</summary>
        private async Task ConnectSerialAsync()
        {
            try
            {
                LogInfo($"Đang mở cổng COM {_config!.ComPort} ({_config.BaudRate} baud)...");

                // SerialPort.Open() là blocking call — chạy trên ThreadPool để không
                // giữ synchronization context (giống pattern await connectTask ở nhánh TCP).
                await Task.Run(OpenSerialPortCore);

                LogInfo($"Mở cổng COM thành công: {_config.ComPort} ({_config.BaudRate} baud).");
                StartReadTimer();
            }
            catch (Exception ex)
            {
                LogError(ex, "ConnectSerialAsync");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
            }
        }

        /// <summary>
        /// Tạo và mở <see cref="SerialPort"/> theo cấu hình hiện tại. Dùng chung cho cả
        /// kết nối lần đầu (<see cref="ConnectSerialAsync"/>) và reconnect
        /// (<see cref="TryReconnectSerialAsync"/>). Ném exception nếu mở thất bại —
        /// caller chịu trách nhiệm bắt và xử lý.
        /// Parity/DataBits/StopBits cố định None/8/One — đúng chuẩn phổ biến nhất của
        /// cân điện tử RS232 (giống pattern SerialPort trong RfidDriver.OpenSerialPort).
        /// </summary>
        private void OpenSerialPortCore()
        {
            _serialPort = new SerialPort(_config!.ComPort, _config.BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout  = 3000,
                WriteTimeout = 3000
            };
            _serialPort.Open();
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
            if (!_timerSemaphore.Wait(0))
            {
                LogInfo("[Timer] Semaphore busy — bỏ qua tick này.");
                return;
            }

            try
            {
                // Kiểm tra kết nối — đúng field theo ConnectionType đang dùng
                if (!IsConnected)
                {
                    LogInfo($"[Timer] Mất kết nối ({_config!.ConnectionType}) — khởi động auto-reconnect.");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    StartReconnectLoop();   // Reconnect loop sẽ restart timer sau khi thành công
                    return;                 // Không restart timer ở đây
                }

                LogInfo("[Timer] Đang đọc dữ liệu cân...");
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
        // READ & PARSE (dispatcher theo ConnectionType)
        // ===================================================
        private Task ReadScaleDataAsync() =>
            _config!.ConnectionType == ScaleConnectionType.Com
                ? ReadScaleDataFromSerialAsync()
                : ReadScaleDataFromTcpAsync();

        // ===================================================
        // READ & PARSE — TCP
        // ===================================================
        private async Task ReadScaleDataFromTcpAsync()
        {
            try
            {
                // Tạo NetworkStream + StreamReader mỗi lần đọc (không giữ persistent)
                // ownsSocket: false → NetworkStream.Dispose() không đóng socket
                using var stream = new NetworkStream(_socket!, ownsSocket: false);
                using var reader = new StreamReader(stream);

                var readTask    = reader.ReadLineAsync();
                var timeoutTask = Task.Delay(3000);

                if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask)
                {
                    LogInfo("Timeout đọc dữ liệu từ cân.");
                    // readTask bị bỏ, stream/reader sẽ bị dispose khi using kết thúc
                    return;
                }

                RawData = await readTask;

                // null = EOF — scale đóng kết nối từ phía nó (FIN packet)
                // TcpClient.Connected không phát hiện được EOF nên phải check thủ công
                if (RawData == null)
                {
                    LogInfo("[Read] EOF nhận được — scale đã đóng kết nối. Trigger reconnect.");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    StartReconnectLoop();
                    return;
                }

                if (string.IsNullOrWhiteSpace(RawData))
                {
                    LogInfo("[Read] Dòng rỗng — bỏ qua.");
                    return;
                }

                // ── DRAIN BACKLOG (FIX: giá trị hiển thị bị "trễ" so với cân thật) ──
                // Một số cân (vd. Shimadzu TX4202L ở chế độ auto-print) gửi dữ liệu
                // liên tục NHANH HƠN chu kỳ đọc TimeScanMs (vd. 400ms). Nếu chỉ đọc
                // đúng 1 dòng mỗi tick, dữ liệu sẽ tồn đọng trong buffer TCP và NGÀY
                // CÀNG LỆCH XA thời điểm hiện tại — vì mỗi tick chỉ "rút" được 1 dòng
                // trong khi cân đã "đẩy" thêm nhiều dòng mới vào buffer.
                // → Đọc hết các dòng ĐÃ CÓ SẴN trong buffer, chỉ giữ lại dòng CUỐI
                //   CÙNG (mới nhất) để parse — đảm bảo giá trị hiển thị luôn bắt kịp
                //   real-time thay vì hiển thị dữ liệu cũ.
                //
                // FIX (đọc đứt gãy giữa số, vd. "113.44g" → chỉ còn ".44g"):
                //   Bản đầu dùng Task.WhenAny(read, Task.Delay(20)) rồi "bỏ dở" read
                //   nếu timeout thắng. Nhưng read bị bỏ dở đó VẪN chạy ngầm và có thể
                //   hoàn tất SAU KHI tick này đã dispose stream/reader — đúng lúc tick
                //   KẾ TIẾP tạo StreamReader MỚI trên CÙNG 1 socket → 2 lần đọc chạy
                //   song song, mỗi bên "cướp" một phần byte của cùng 1 dòng dữ liệu.
                //   → Dùng Socket.Available (kiểm tra đồng bộ, không I/O) để biết CHẮC
                //   đã có dữ liệu trong buffer trước khi đọc, và luôn AWAIT trọn vẹn
                //   (không bao giờ bỏ dở) — không còn 2 read chạy song song.
                while (_socket!.Available > 0)
                {
                    var extra = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(extra))
                        break; // EOF hoặc dòng rỗng — dừng, giữ RawData hiện tại

                    RawData = extra; // Ghi đè bằng dòng mới hơn — luôn giữ bản mới nhất
                }

                ParseScaleData(RawData);
                LogInfo($"[Read OK] {_weightKg:F3} {_unit} (raw: '{RawData.Trim()}')");
                SetDataValue(new DataValue(DriverStatus.Connected, _weightKg));
            }
            catch (Exception ex)
            {
                // Lỗi network → báo Disconnected và khởi động reconnect
                LogError(ex, "ReadScaleDataFromTcpAsync");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                StartReconnectLoop();
            }
        }

        // ===================================================
        // READ & PARSE — COM
        // ===================================================
        private async Task ReadScaleDataFromSerialAsync()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    LogInfo("[Read-COM] SerialPort chưa mở — bỏ qua tick.");
                    return;
                }

                // StreamReader trên SerialPort.BaseStream, leaveOpen:true — KHÔNG đóng
                // BaseStream khi reader bị dispose cuối using, vì _serialPort là object
                // persistent giữ suốt 1 kết nối (giống ownsSocket:false ở nhánh TCP,
                // tránh phải mở/đóng cổng COM mỗi tick).
                using var reader = new StreamReader(_serialPort.BaseStream, Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

                var readTask    = reader.ReadLineAsync();
                var timeoutTask = Task.Delay(3000);

                if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask)
                {
                    LogInfo("Timeout đọc dữ liệu từ cân (COM).");
                    // readTask bị bỏ, reader sẽ bị dispose khi using kết thúc (không đóng port)
                    return;
                }

                RawData = await readTask;

                // null = EOF trên BaseStream — hiếm gặp với SerialPort (không có khái niệm
                // "đóng kết nối từ xa" như TCP FIN), nhưng vẫn xử lý phòng hờ để nhất quán
                // với nhánh TCP thay vì để null lọt xuống ParseScaleData bên dưới.
                if (RawData == null)
                {
                    LogInfo("[Read-COM] EOF nhận được từ cổng COM — trigger reconnect.");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    StartReconnectLoop();
                    return;
                }

                if (string.IsNullOrWhiteSpace(RawData))
                {
                    LogInfo("[Read-COM] Dòng rỗng — bỏ qua.");
                    return;
                }

                // Drain backlog — cùng chiến lược "luôn giữ dòng mới nhất" như nhánh TCP
                // (xem giải thích chi tiết ở ReadScaleDataFromTcpAsync). SerialPort.BytesToRead
                // là kiểm tra đồng bộ, không I/O — an toàn để dùng thay Socket.Available.
                while (_serialPort.BytesToRead > 0)
                {
                    var extra = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(extra))
                        break; // EOF hoặc dòng rỗng — dừng, giữ RawData hiện tại

                    RawData = extra; // Ghi đè bằng dòng mới hơn — luôn giữ bản mới nhất
                }

                ParseScaleData(RawData);
                LogInfo($"[Read OK][COM] {_weightKg:F3} {_unit} (raw: '{RawData.Trim()}')");
                SetDataValue(new DataValue(DriverStatus.Connected, _weightKg));
            }
            catch (Exception ex)
            {
                // Lỗi cổng COM (rút dây, tắt máy chuyển đổi USB...) → Disconnected + reconnect
                LogError(ex, "ReadScaleDataFromSerialAsync");
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

                if (_stabilityDebugField != null)
                    StabilityDebugInfo = (_stabilityDebugField.GetValue(null) as string) ?? "";
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
        /// Dừng read-timer, chuyển sang Reconnecting và thử kết nối lại (đúng
        /// ConnectionType đang dùng — TCP hoặc COM) mỗi <see cref="ReconnectDelayMs"/> ms
        /// cho đến khi thành công hoặc dispose.
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

                    LogInfo($"[Reconnect] Đang thử kết nối lại cân ({_config!.ConnectionType})...");
                    bool ok = _config.ConnectionType == ScaleConnectionType.Com
                        ? await TryReconnectSerialAsync()
                        : await TryReconnectTcpAsync();

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
                // Đóng socket/client cũ
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
                    // Observe connectTask để tránh UnobservedTaskException
                    _ = connectTask.ContinueWith(t => { _ = t.Exception; },
                            TaskContinuationOptions.OnlyOnFaulted);
                    return false;
                }

                await connectTask;
                _socket = _tcpClient.Client;

                SetDataValue(new DataValue(DriverStatus.Connected, 0.0));
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "TryReconnectTcpAsync");
                return false;
            }
        }

        /// <summary>Đóng cổng COM cũ (nếu có) và thử mở lại.</summary>
        private async Task<bool> TryReconnectSerialAsync()
        {
            try
            {
                ClosePort();
                await Task.Run(OpenSerialPortCore);

                SetDataValue(new DataValue(DriverStatus.Connected, 0.0));
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "TryReconnectSerialAsync");
                return false;
            }
        }

        // ===================================================
        // DISCONNECT
        // ===================================================

        /// <summary>Dừng timer, hủy reconnect loop và đóng kết nối (TCP hoặc COM).</summary>
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

                // Nhánh TCP
                _socket?.Close();
                _socket = null;

                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;

                // Nhánh COM
                ClosePort();

                SetDataValue(new DataValue(DriverStatus.Disconnected, null));
                LogInfo("ScaleDriver đã ngắt kết nối.");
            }
            catch (Exception ex)
            {
                LogError(ex, "Disconnect");
            }
        }

        /// <summary>Đóng và giải phóng <see cref="_serialPort"/> nếu đang mở. An toàn khi gọi nhiều lần.</summary>
        private void ClosePort()
        {
            try
            {
                if (_serialPort == null) return;
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
            }
            catch (Exception ex)
            {
                LogError(ex, "ClosePort");
            }
            finally
            {
                _serialPort = null;
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
