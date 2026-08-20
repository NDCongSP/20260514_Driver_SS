// ============================================================
// File: Servers/TcpScaleServer.cs
// Mục đích: Mô phỏng cân xuất RS232 qua bộ chuyển đổi Serial-to-Ethernet —
//           mở 1 TCP server, accept nhiều client, phát lại (broadcast) mỗi
//           dòng raw data engine sinh ra tới TẤT CẢ client đang kết nối.
//           ScaleDriver (ConnectionType=Tcp) chỉ cần trỏ IP=127.0.0.1,
//           Port=cổng này là đọc được y hệt cân thật.
// ============================================================

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScaleSimulator.Servers
{
    public sealed class TcpScaleServer : IDisposable
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly List<TcpClient> _clients = new();
        private readonly object _clientsLock = new();

        public event Action<string>? Log;

        public bool IsRunning { get; private set; }
        public int ClientCount { get { lock (_clientsLock) return _clients.Count; } }

        public void Start(int port)
        {
            if (IsRunning) return;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _cts = new CancellationTokenSource();
            IsRunning = true;

            Log?.Invoke($"[TCP] Đang lắng nghe trên port {port}...");
            _ = AcceptLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            try { _listener?.Stop(); } catch { /* ignored */ }
            _listener = null;

            lock (_clientsLock)
            {
                foreach (var c in _clients)
                {
                    try { c.Close(); } catch { /* ignored */ }
                }
                _clients.Clear();
            }

            Log?.Invoke("[TCP] Đã dừng server.");
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await _listener!.AcceptTcpClientAsync(token);
                    lock (_clientsLock) { _clients.Add(client); }

                    var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
                    Log?.Invoke($"[TCP] Client kết nối: {remote} (tổng: {ClientCount})");

                    // Dọn client khi nó tự đóng — theo dõi ngầm, không chặn accept loop.
                    _ = MonitorClientAsync(client, token);
                }
            }
            catch (OperationCanceledException) { /* Stop() được gọi — bình thường */ }
            catch (ObjectDisposedException) { /* listener đã Stop() — bình thường */ }
            catch (Exception ex)
            {
                Log?.Invoke($"[TCP][ERROR] Accept loop: {ex.Message}");
            }
        }

        private async Task MonitorClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                var buffer = new byte[1];
                while (!token.IsCancellationRequested && client.Connected)
                {
                    // Peek nhẹ nhàng: nếu client đóng, Socket báo readable với 0 byte.
                    if (client.Client.Poll(500_000, SelectMode.SelectRead) && client.Client.Available == 0)
                        break;
                    await Task.Delay(500, token);
                }
            }
            catch { /* ignored — client rớt bất thường cũng dọn giống nhau */ }
            finally
            {
                lock (_clientsLock) { _clients.Remove(client); }
                try { client.Close(); } catch { /* ignored */ }
                Log?.Invoke($"[TCP] Client ngắt kết nối (còn lại: {ClientCount})");
            }
        }

        /// <summary>Gửi 1 dòng raw data (kèm CRLF) tới TẤT CẢ client đang kết nối.</summary>
        public void Broadcast(string line)
        {
            if (!IsRunning) return;

            byte[] data = Encoding.ASCII.GetBytes(line + "\r\n");
            List<TcpClient> deadClients = new();

            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch
                    {
                        deadClients.Add(client);
                    }
                }
                foreach (var dead in deadClients) _clients.Remove(dead);
            }
        }

        public void Dispose() => Stop();
    }
}
