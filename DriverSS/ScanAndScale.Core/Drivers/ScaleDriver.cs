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
// DLL model cân (Scale_DIGI.dll, v.v.) phải nằm cùng thư mục
// với ứng dụng đang chạy (hoặc chỉ định đường dẫn trong ScaleConfig).
//
// QUAN TRỌNG VỀ THREAD:
//   Timer callback và đọc dữ liệu chạy trên ThreadPool thread.
//   Trong WPF: dùng Application.Current.Dispatcher.Invoke(...)
//   Trong WinForms: dùng control.Invoke(...)
// ============================================================

using ScanAndScale.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
// System.Runtime.Loader chỉ có trên .NET Core / .NET 5+ (không có trên .NET Framework)
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace ScanAndScale.Core.Drivers
{
    /// <summary>
    /// Driver đọc giá trị cân điện tử qua kết nối TCP/IP.
    /// <para>
    /// KHÔNG phải Singleton — mỗi cân là một instance riêng biệt.
    /// Một ứng dụng có thể có nhiều cân cùng lúc (nhiều IP khác nhau).
    /// </para>
    /// <para>
    /// Cách dùng:
    /// <code>
    ///   var driver = new ScaleDriver();
    ///   driver.DataValueChanged += (s, e) => {
    ///     double weight = (double)(e.NewValue.Value ?? 0.0);
    ///     Console.WriteLine($"Cân: {weight} KG");
    ///   };
    ///   driver.Initialize(new ScaleConfig {
    ///       Enable = true,
    ///       IP = "192.168.80.237",
    ///       Port = 23,
    ///       ModelName = "Scale_DIGI",
    ///       TimeScanMs = 400
    ///   });
    ///   // Khi thoát:
    ///   driver.Dispose();
    /// </code>
    /// </para>
    /// </summary>
    public class ScaleDriver : IDisposable
    {
        // ===================================================
        // FIELDS — Biến nội bộ
        // ===================================================

        // Kết nối TCP đến cân
        private TcpClient? _tcpClient;
        private Socket? _socket;

        // Timer đọc dữ liệu định kỳ
        private System.Timers.Timer? _readTimer;

        // Config hiện tại
        private ScaleConfig? _config;

        // Reflection objects cho model cân (load từ DLL)
        private object? _scaleModelInstance;     // Instance của class ScaleReading
        private MethodInfo? _getWeightMethod;    // Phương thức GetWeight

        // Trạng thái hiện tại
        private DataValue _currentDataValue = new DataValue(DriverStatus.Unknown, null);

        // Giá trị parse từ cân
        private double _weightKg;       // Giá trị cân (KG)
        private bool _isStable;         // Cân ổn định không
        private bool _isTare;           // Cân đang ở trạng thái tare không
        private string _unit = "KG";    // Đơn vị

        // Lock object cho timer (tránh đọc đồng thời)
        private readonly object _timerLock = new object();

        // Đã dispose chưa
        private bool _disposed = false;

        // Dữ liệu thô nhận được (để debug)
        public string? RawData { get; private set; }

        // ===================================================
        // PROPERTIES CÔNG KHAI
        // ===================================================

        /// <summary>Giá trị cân hiện tại (DataValue.Value = double kg).</summary>
        public DataValue CurrentValue => _currentDataValue;

        /// <summary>Cân có đang kết nối không.</summary>
        public bool IsConnected => _tcpClient?.Connected == true;

        /// <summary>Cân đang ổn định không (Stable flag từ protocol cân).</summary>
        public bool IsStable => _isStable;

        /// <summary>Cân đang ở trạng thái Tare không.</summary>
        public bool IsTare => _isTare;

        /// <summary>Đơn vị hiện tại của cân (KG, G, TON).</summary>
        public string Unit => _unit;

        // ===================================================
        // EVENTS
        // ===================================================

        private EventHandler<DataValueChangedEventArgs>? _dataValueChanged;

        /// <summary>
        /// Sự kiện kích hoạt khi giá trị cân thay đổi.
        /// <para>⚠️ Chạy trên ThreadPool thread — cần dispatch về UI thread khi cập nhật UI.</para>
        /// </summary>
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }

        // ===================================================
        // CONSTRUCTOR
        // ===================================================
        public ScaleDriver() { }

        // ===================================================
        // KHỞI TẠO
        // ===================================================

        /// <summary>
        /// Khởi tạo Scale Driver: tải DLL model cân, kết nối TCP, và bắt đầu timer đọc.
        /// </summary>
        /// <param name="config">Cấu hình cân. Null = dùng mặc định.</param>
        public void Initialize(ScaleConfig? config = null)
        {
            _config = config ?? new ScaleConfig();

            // Kiểm tra enable
            if (!_config.Enable)
            {
                LogInfo("ScaleDriver bị vô hiệu hóa (Enable=false).");
                return;
            }

            // Bước 1: Tải DLL model cân và chuẩn bị reflection
            if (!LoadScaleModel())
            {
                LogInfo($"Không tải được DLL model cân: {_config.ModelName}.dll");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                return;
            }

            // Bước 2: Kết nối TCP đến cân (async để không block)
            _ = ConnectAsync();
        }

        // ===================================================
        // TẢI DLL MODEL CÂN (Reflection)
        // ===================================================

        /// <summary>
        /// Tải DLL model cân tương ứng với <see cref="ScaleConfig.ModelName"/>.
        /// Dùng Reflection để gọi hàm GetWeight() trong DLL đó.
        /// <para>
        /// Ưu tiên 1 — Embedded resource: Scale DLL được nhúng vào ScanAndScale.Core.dll
        ///   khi build (EmbeddedResource trong .csproj). Không cần file bên ngoài.
        /// Ưu tiên 2 — File trên disk: tìm trong thư mục .exe / thư mục hiện tại.
        ///   Dùng khi Scale DLL được deploy thủ công hoặc trong môi trường dev.
        /// </para>
        /// <para>
        /// DLL cần có class: {ModelName}.ScaleReading
        /// với method: GetWeight(out double?, out bool?, out bool?, out string, string)
        /// </para>
        /// </summary>
        private bool LoadScaleModel()
        {
            try
            {
                string dllFileName = $"{_config!.ModelName}.dll";

                // ── Ưu tiên 1: Load từ EmbeddedResource ─────────────────────────────
                // Scale DLL được nhúng vào ScanAndScale.Core.dll khi build.
                // Cách này hoạt động ngay cả khi không có file Scale_*.dll bên ngoài.
                var assembly = LoadAssemblyFromEmbeddedResource(dllFileName);

                if (assembly != null)
                {
                    LogInfo($"Load model cân từ embedded resource: {dllFileName}");
                }
                else
                {
                    // Diagnostic: liệt kê resource thực sự có trong Core.dll
                    // Nếu danh sách rỗng → EmbedScaleDlls target chưa nhúng được DLL.
                    var available = typeof(ScaleDriver).Assembly.GetManifestResourceNames();
                    if (available.Length == 0)
                        LogInfo($"[WARN] Không có embedded resource nào trong Core.dll — EmbedScaleDlls target có thể chưa chạy đúng. Cần Rebuild Solution.");
                    else
                        LogInfo($"[WARN] Không tìm thấy '{dllFileName}'. Resources trong Core.dll: {string.Join(", ", available)}");

                    // ── Ưu tiên 2: Load từ file trên disk (fallback) ─────────────────
                    // Dùng khi Scale DLL không được nhúng (build cũ, hoặc deploy thủ công).
                    string dllFullPath = FindDllPath(dllFileName);

                    if (!File.Exists(dllFullPath))
                    {
                        LogInfo($"Không tìm thấy embedded resource hoặc file: {dllFileName}");
                        return false;
                    }

#if NETFRAMEWORK
                    assembly = Assembly.LoadFrom(dllFullPath);
#else
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllFullPath);
#endif
                    LogInfo($"Load model cân từ file: {dllFullPath}");
                }

                // ── Lấy class ScaleReading và method GetWeight via Reflection ────────
                string typeName = $"{_config.ModelName}.ScaleReading";
                var scaleType = assembly.GetType(typeName);

                if (scaleType == null)
                {
                    LogInfo($"Không tìm thấy class '{typeName}' trong DLL.");
                    return false;
                }

                // Signature: GetWeight(out double?, out bool?, out bool?, out string, string)
                _getWeightMethod = scaleType.GetMethod("GetWeight", new Type[]
                {
                    typeof(double?).MakeByRefType(),   // out double? WeightValue
                    typeof(bool?).MakeByRefType(),     // out bool? Stable
                    typeof(bool?).MakeByRefType(),     // out bool? Tare
                    typeof(string).MakeByRefType(),    // out string Unit
                    typeof(string)                     // string rawData (input)
                });

                if (_getWeightMethod == null)
                {
                    LogInfo($"Không tìm thấy method 'GetWeight' với signature đúng trong '{typeName}'.");
                    return false;
                }

                _scaleModelInstance = Activator.CreateInstance(scaleType);
                LogInfo($"Load model cân thành công: {_config.ModelName}");
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "ScaleDriver.LoadScaleModel");
                return false;
            }
        }

        /// <summary>
        /// Thử load assembly từ EmbeddedResource trong ScanAndScale.Core.dll.
        /// <para>
        /// Scale DLL được nhúng với LogicalName = "Scale_DIGI.dll" (tên file đơn giản,
        /// không có namespace prefix) bởi target EmbedScaleDlls trong .csproj.
        /// </para>
        /// </summary>
        /// <param name="dllFileName">Tên file DLL, ví dụ "Scale_DIGI.dll"</param>
        /// <returns>Assembly nếu tìm thấy embedded resource, null nếu không có.</returns>
        private static System.Reflection.Assembly? LoadAssemblyFromEmbeddedResource(string dllFileName)
        {
            try
            {
                // Lấy stream của embedded resource từ ScanAndScale.Core.dll
                // LogicalName trong .csproj được đặt là "Scale_DIGI.dll" (tên file đơn giản)
                var coreAssembly = typeof(ScaleDriver).Assembly;
                using var stream = coreAssembly.GetManifestResourceStream(dllFileName);

                if (stream == null)
                    return null; // Không có embedded resource — dùng fallback file

                // Đọc toàn bộ bytes của DLL
                var bytes = new byte[stream.Length];
                var totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    var read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }

                // Load assembly từ byte array — không cần file trên disk
                // Scale DLL chỉ dùng BCL (Regex, Math) nên load từ bytes hoạt động tốt
                return System.Reflection.Assembly.Load(bytes);
            }
            catch
            {
                return null; // Lỗi khi load embedded → fallback sang file
            }
        }

        /// <summary>
        /// Tìm đường dẫn đầy đủ của file DLL trên disk.
        /// Dùng làm fallback khi không có embedded resource.
        /// Ưu tiên thư mục của process, sau đó thư mục hiện tại.
        /// </summary>
        private static string FindDllPath(string dllFileName)
        {
            // Thử 1: Cùng thư mục với file thực thi của ứng dụng
            string? exeDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "");
            if (!string.IsNullOrEmpty(exeDir))
            {
                string path1 = Path.Combine(exeDir, dllFileName);
                if (File.Exists(path1)) return path1;
            }

            // Thử 2: Thư mục hiện tại
            string path2 = Path.GetFullPath(dllFileName);
            return path2;
        }

        // ===================================================
        // KẾT NỐI TCP
        // ===================================================

        /// <summary>
        /// Kết nối TCP đến địa chỉ IP:Port của cân (async).
        /// Sau khi kết nối thành công, tự động start timer đọc dữ liệu.
        /// </summary>
        private async Task ConnectAsync()
        {
            try
            {
                LogInfo($"Đang kết nối TCP đến {_config!.IP}:{_config.Port}...");

                _tcpClient = new TcpClient();

                // Timeout kết nối 5 giây
                var connectTask = _tcpClient.ConnectAsync(_config.IP, _config.Port);
                var timeoutTask = Task.Delay(5000);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    LogInfo($"Kết nối TCP timeout sau 5 giây. IP: {_config.IP}");
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    return;
                }

                await connectTask; // Chắc chắn task hoàn thành (có thể throw exception)

                _socket = _tcpClient.Client;

                LogInfo($"Kết nối TCP thành công: {_config.IP}:{_config.Port}");

                // Bắt đầu timer đọc dữ liệu định kỳ
                StartReadTimer();
            }
            catch (Exception ex)
            {
                LogError(ex, "ScaleDriver.ConnectAsync");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
            }
        }

        // ===================================================
        // TIMER ĐỌC DỮ LIỆU ĐỊNH KỲ
        // ===================================================

        /// <summary>Bắt đầu timer đọc dữ liệu từ cân.</summary>
        private void StartReadTimer()
        {
            _readTimer = new System.Timers.Timer(_config!.TimeScanMs);
            _readTimer.Elapsed += OnReadTimerElapsed;
            _readTimer.AutoReset = false; // Tắt auto-reset, tự start lại sau mỗi lần đọc
            _readTimer.Start();
            LogInfo($"Timer đọc cân khởi động (interval: {_config.TimeScanMs}ms).");
        }

        /// <summary>
        /// Callback của timer — được gọi mỗi chu kỳ TimeScanMs.
        /// ⚠️ Chạy trên ThreadPool thread!
        /// </summary>
        private async void OnReadTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // Sử dụng lock để tránh đọc đồng thời khi timer chạy chậm hơn interval
            if (!Monitor.TryEnter(_timerLock)) return;

            try
            {
                // Kiểm tra trạng thái kết nối TCP
                if (_tcpClient == null || !_tcpClient.Connected)
                {
                    SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
                    return;
                }

                // Đọc dữ liệu từ cân
                await ReadScaleDataAsync();
            }
            catch (Exception ex)
            {
                LogError(ex, "OnReadTimerElapsed");
                SetDataValue(new DataValue(DriverStatus.Disconnected, 0.0));
            }
            finally
            {
                Monitor.Exit(_timerLock);

                // Restart timer (sau khi đọc xong mới đọc lại, tránh chồng chéo)
                if (!_disposed && _readTimer != null)
                {
                    _readTimer.Start();
                }
            }
        }

        // ===================================================
        // ĐỌC VÀ PARSE DỮ LIỆU CÂN
        // ===================================================

        /// <summary>
        /// Đọc một dòng dữ liệu thô từ TCP stream và gọi DLL model cân để parse.
        /// </summary>
        private async Task ReadScaleDataAsync()
        {
            try
            {
                // Tạo NetworkStream từ socket đang kết nối
                using var stream = new NetworkStream(_socket!);
                using var reader = new StreamReader(stream);

                // Đọc một dòng dữ liệu từ cân (ReadLine chờ đến khi gặp '\n')
                var readTask = reader.ReadLineAsync();
                var timeoutTask = Task.Delay(3000); // Timeout 3 giây

                if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask)
                {
                    LogInfo("Timeout đọc dữ liệu từ cân.");
                    return;
                }

                RawData = await readTask;

                if (string.IsNullOrEmpty(RawData))
                    return;

                // Gọi DLL model cân để parse dữ liệu thô
                ParseScaleData(RawData);

                // Cập nhật giá trị và bắn sự kiện
                SetDataValue(new DataValue(DriverStatus.Connected, _weightKg));
            }
            catch (Exception ex)
            {
                LogError(ex, "ReadScaleDataAsync");
            }
        }

        /// <summary>
        /// Gọi method GetWeight() trong DLL model cân để parse dữ liệu thô.
        /// Dùng Reflection vì mỗi model cân có DLL riêng với logic parse khác nhau.
        /// </summary>
        /// <param name="rawData">Chuỗi dữ liệu thô nhận từ cân qua TCP.</param>
        private void ParseScaleData(string rawData)
        {
            try
            {
                if (_getWeightMethod == null || _scaleModelInstance == null)
                    return;

                // Chuẩn bị các tham số (out params phải khởi tạo trước)
                object?[] parameters = new object?[]
                {
                    null,   // out double? WeightValue
                    null,   // out bool? Stable
                    null,   // out bool? Tare
                    "",     // out string Unit
                    rawData // string rawData (input)
                };

                // Gọi GetWeight() trong DLL model cân
                _getWeightMethod.Invoke(_scaleModelInstance, parameters);

                // Lấy kết quả từ các out parameters
                var rawWeight = (double?)parameters[0] ?? 0.0;
                _isStable = (bool?)parameters[1] ?? false;
                _isTare = (bool?)parameters[2] ?? false;
                _unit = (string?)parameters[3] ?? "Kg";
                _weightKg = rawWeight;
            }
            catch (Exception ex)
            {
                LogError(ex, "ScaleDriver.ParseScaleData");
            }
        }

        // ===================================================
        // CẬP NHẬT GIÁ TRỊ & BẮN SỰ KIỆN
        // ===================================================

        /// <summary>
        /// Cập nhật <see cref="_currentDataValue"/> và bắn sự kiện <see cref="DataValueChanged"/>
        /// nếu giá trị hoặc trạng thái thực sự thay đổi.
        /// </summary>
        private void SetDataValue(DataValue newValue)
        {
            if (_currentDataValue.Equals(newValue))
                return;

            var oldValue = _currentDataValue;
            _currentDataValue = newValue;
            _dataValueChanged?.Invoke(this, new DataValueChangedEventArgs(oldValue, newValue));
        }

        // ===================================================
        // NGẮT KẾT NỐI
        // ===================================================

        /// <summary>Ngắt kết nối TCP và dừng timer đọc.</summary>
        public void Disconnect()
        {
            try
            {
                _readTimer?.Stop();
                _readTimer?.Dispose();
                _readTimer = null;

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
                LogError(ex, "ScaleDriver.Disconnect");
            }
        }

        // ===================================================
        // DISPOSE
        // ===================================================

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
            GC.SuppressFinalize(this);
        }

        // ===================================================
        // HELPERS — LOG
        // ===================================================

        private static void LogInfo(string msg) =>
            Debug.WriteLine($"[ScaleDriver] {msg}");

        private static void LogError(Exception ex, string context) =>
            Debug.WriteLine($"[ScaleDriver][ERROR] {context}: {ex.Message}");
    }
}
