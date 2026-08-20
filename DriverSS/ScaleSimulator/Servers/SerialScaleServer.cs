// ============================================================
// File: Servers/SerialScaleServer.cs
// Mục đích: Mô phỏng cân cắm trực tiếp cổng COM (RS232/USB-to-Serial) —
//           mở 1 SerialPort và ghi mỗi dòng raw data engine sinh ra ra port đó.
//
// QUAN TRỌNG — cần cặp cổng COM ảo (null-modem) để test trên 1 máy:
//   Windows không tự có 2 cổng COM "nói chuyện" được với nhau như
//   TCP loopback (127.0.0.1). Cần 1 trong 2 cách:
//     (1) Dùng phần mềm tạo cặp cổng COM ảo null-modem, ví dụ com0com
//         (miễn phí, mã nguồn mở) — tạo ra cặp vd COM5 <-> COM6, mọi byte
//         ghi vào COM5 sẽ đọc được ở COM6 và ngược lại.
//     (2) Có 2 cổng COM thật (RS232) trên máy, nối với nhau bằng cáp
//         null-modem (chéo TX/RX) — cùng nguyên lý (1) nhưng bằng phần cứng.
//   → Mở SerialScaleServer ở 1 đầu cặp (vd COM5), rồi trỏ
//     ScaleConfig.ComPort của app đang test sang đầu còn lại (COM6).
// ============================================================

using System.IO.Ports;

namespace ScaleSimulator.Servers
{
    public sealed class SerialScaleServer : IDisposable
    {
        private SerialPort? _port;

        public event Action<string>? Log;

        public bool IsRunning => _port?.IsOpen == true;

        public void Start(string comPort, int baudRate)
        {
            if (IsRunning) return;

            try
            {
                // Parity/DataBits/StopBits None/8/One — khớp đúng cấu hình cố định mà
                // ScanAndScale.Core/Drivers/ScaleDriver.cs dùng (OpenSerialPortCore),
                // để dữ liệu simulator phát ra luôn tương thích với driver thật.
                _port = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One)
                {
                    WriteTimeout = 3000
                };
                _port.Open();
                Log?.Invoke($"[COM] Đã mở {comPort} ({baudRate} baud).");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM][ERROR] Không mở được {comPort}: {ex.Message}");
                _port?.Dispose();
                _port = null;
            }
        }

        public void Stop()
        {
            if (_port == null) return;

            try
            {
                if (_port.IsOpen) _port.Close();
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM][ERROR] Đóng cổng: {ex.Message}");
            }
            finally
            {
                _port.Dispose();
                _port = null;
                Log?.Invoke("[COM] Đã đóng cổng.");
            }
        }

        /// <summary>Ghi 1 dòng raw data (kèm CRLF) ra cổng COM đang mở.</summary>
        public void Write(string line)
        {
            if (!IsRunning) return;

            try
            {
                _port!.Write(line + "\r\n");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM][ERROR] Ghi dữ liệu thất bại: {ex.Message}");
            }
        }

        public void Dispose() => Stop();
    }
}
