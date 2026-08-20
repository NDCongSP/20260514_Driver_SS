// ============================================================
// File: ScaleSimEngine.cs
// Mục đích: "Trái tim" của simulator — timer chạy nền, mỗi tick tính giá trị
//           cân hiện tại (target weight + nhiễu ngẫu nhiên nếu có), build
//           dòng raw data theo format model đang chọn, rồi bắn sự kiện
//           LineGenerated để TcpScaleServer/SerialScaleServer (và UI) tiêu thụ.
//
//           DÙNG CHUNG 1 engine cho cả TCP và COM — đúng thực tế 1 cân thật
//           chỉ có 1 luồng dữ liệu, TCP và COM chỉ là 2 "đường ống" phát lại
//           CÙNG dữ liệu đó (hữu ích nếu muốn test song song 2 driver instance).
// ============================================================

using ScaleSimulator.ScaleFormats;

namespace ScaleSimulator
{
    public sealed class ScaleSimEngine : IDisposable
    {
        private readonly System.Threading.Timer _timer;
        private readonly Random _rng = new();
        private readonly object _lock = new();

        private ScaleFormatDefinition _format = ScaleFormatCatalog.Digi;
        private double _targetWeight;
        private double _noise;
        private string? _unit;
        private string? _rawFlag;
        private int _intervalMs = 400;

        public event Action<string>? LineGenerated;
        public event Action<double>? EffectiveWeightChanged;

        public ScaleSimEngine()
        {
            // dueTime=Infinite lúc khởi tạo — chỉ chạy khi Start() được gọi.
            _timer = new System.Threading.Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public bool IsRunning { get; private set; }

        public void Configure(ScaleFormatDefinition format, double targetWeight, double noise,
            string? unit, string? rawFlag, int intervalMs)
        {
            lock (_lock)
            {
                _format = format;
                _targetWeight = targetWeight;
                _noise = Math.Max(0, noise);
                _unit = unit;
                _rawFlag = rawFlag;
                _intervalMs = Math.Max(20, intervalMs);
            }

            // Nếu đang chạy, áp dụng interval mới ngay (không cần Stop/Start lại).
            if (IsRunning)
                _timer.Change(0, _intervalMs);
        }

        /// <summary>Đặt riêng target weight — dùng cho ramp mô phỏng "đặt vật lên cân".</summary>
        public void SetTargetWeight(double targetWeight)
        {
            lock (_lock) { _targetWeight = targetWeight; }
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _timer.Change(0, _intervalMs);
        }

        public void Stop()
        {
            IsRunning = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTick(object? state)
        {
            ScaleFormatDefinition format;
            double target, noise;
            string? unit, rawFlag;

            lock (_lock)
            {
                format = _format;
                target = _targetWeight;
                noise = _noise;
                unit = _unit;
                rawFlag = _rawFlag;
            }

            // Nhiễu ngẫu nhiên đối xứng quanh target — mô phỏng dao động thật của cân.
            double effective = noise > 0
                ? target + (_rng.NextDouble() * 2 - 1) * noise
                : target;

            string line = format.BuildLine(new ScaleBuildContext
            {
                Weight = effective,
                Unit = unit,
                RawFlag = rawFlag
            });

            EffectiveWeightChanged?.Invoke(effective);
            LineGenerated?.Invoke(line);
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
        }
    }
}
