// ============================================================
// File: MainForm.cs
// Mục đích: UI chính của ScaleSimulator — cấu hình model/giá trị/cờ trạng
//           thái cân, bật/tắt TCP server và/hoặc COM writer, xem log.
//
// Ghi chú: Toàn bộ layout dựng bằng code trong BuildUi() (không có
//          MainForm.Designer.cs riêng) — đơn giản hơn cho 1 tool test nội
//          bộ, không phụ thuộc WinForms Designer của Visual Studio. Vẫn mở
//          và chạy bình thường trong VS.
// ============================================================

using System.IO.Ports;
using ScaleSimulator.ScaleFormats;
using ScaleSimulator.Servers;

namespace ScaleSimulator
{
    public partial class MainForm : Form
    {
        private readonly ScaleSimEngine _engine = new();
        private readonly TcpScaleServer _tcpServer = new();
        private readonly SerialScaleServer _serialServer = new();

        // ─── Controls (khai báo field vì được truy cập ở nhiều handler) ───
        private ComboBox _cboModel = null!;
        private NumericUpDown _numWeight = null!;
        private NumericUpDown _numNoise = null!;
        private ComboBox _cboUnit = null!;
        private ComboBox _cboFlag = null!;
        private NumericUpDown _numInterval = null!;
        private Label _lblNote = null!;
        private TextBox _txtPreview = null!;
        private Button _btnAutoRamp = null!;

        private NumericUpDown _numTcpPort = null!;
        private Button _btnTcpStartStop = null!;
        private Label _lblTcpStatus = null!;

        private ComboBox _cboComPort = null!;
        private Button _btnRefreshCom = null!;
        private ComboBox _cboBaud = null!;
        private Button _btnComStartStop = null!;
        private Label _lblComStatus = null!;

        private CheckBox _chkLogEachLine = null!;
        private TextBox _txtLog = null!;

        public MainForm()
        {
            Text = "Scale Simulator — Mô phỏng cân qua TCP & RS232 (test ScaleDriver)";
            Width = 1040;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 650);

            BuildUi();
            WireEvents();

            _tcpServer.Log += line => AppendLog(line);
            _serialServer.Log += line => AppendLog(line);
            _engine.LineGenerated += OnLineGenerated;

            // FIX: ComboBox.DataSource KHÔNG populate Items đồng bộ cho tới khi control có
            // Handle (thường chỉ xảy ra khi Form được realize, vd lúc Show/Load) — set
            // SelectedIndex ngay trong constructor (Items.Count vẫn = 0 lúc này) ném
            // ArgumentOutOfRangeException. Dời việc chọn model ban đầu sang sự kiện Load,
            // lúc đó Handle đã tồn tại và Items đã được điền từ DataSource.
            Load += (_, _) =>
            {
                if (_cboModel.Items.Count > 0)
                    _cboModel.SelectedIndex = 0; // trigger toàn bộ setup ban đầu qua SelectedIndexChanged
                _engine.Start();
            };
        }

        // ===================================================
        // BUILD UI
        // ===================================================
        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildDataGroup(), 0, 0);
            root.Controls.Add(BuildServersGroup(), 0, 1);
            root.Controls.Add(BuildLogGroup(), 0, 2);
        }

        private GroupBox BuildDataGroup()
        {
            var grp = new GroupBox { Text = "1. Dữ liệu cân", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.AutoSize : SizeType.Percent) { Width = i % 2 == 0 ? 0 : 25 });

            _cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            _cboModel.DataSource = ScaleFormatCatalog.All;
            _cboModel.DisplayMember = nameof(ScaleFormatDefinition.DisplayName);

            _numWeight = new NumericUpDown { Width = 120, DecimalPlaces = 2, Minimum = -9999, Maximum = 9999, Increment = 0.01m };
            _numNoise = new NumericUpDown { Width = 120, DecimalPlaces = 3, Minimum = 0, Maximum = 100, Increment = 0.01m, Value = 0 };
            _cboUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _cboFlag = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            _numInterval = new NumericUpDown { Width = 100, Minimum = 20, Maximum = 60000, Value = 400, Increment = 50 };

            int row = 0;
            AddRow(grid, ref row, "Model cân:", _cboModel, "Đơn vị:", _cboUnit);
            AddRow(grid, ref row, "Giá trị (target):", _numWeight, "Nhiễu ngẫu nhiên (±):", _numNoise);
            AddRow(grid, ref row, "Cờ trạng thái:", _cboFlag, "Chu kỳ gửi (ms):", _numInterval);

            _lblNote = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 54, ForeColor = Color.DimGray };
            _txtPreview = new TextBox { Dock = DockStyle.Top, ReadOnly = true, Font = new Font(FontFamily.GenericMonospace, 10) };
            _btnAutoRamp = new Button { Text = "▶ Mô phỏng đặt vật lên cân (ramp → ổn định)", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 0) };

            var previewLabel = new Label { Text = "Xem trước dòng dữ liệu đang phát:", Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 6, 0, 2) };

            grp.Controls.Add(_btnAutoRamp);
            grp.Controls.Add(_txtPreview);
            grp.Controls.Add(previewLabel);
            grp.Controls.Add(_lblNote);
            grp.Controls.Add(grid);
            return grp;
        }

        private GroupBox BuildServersGroup()
        {
            var grp = new GroupBox { Text = "2 & 3. Kênh phát dữ liệu", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };

            // ── TCP panel ──
            // LƯU Ý: KHÔNG dùng AutoSize=true ở đây — GroupBox.AutoSize + con Dock=Top bên
            // trong tạo ra vòng phụ thuộc kích thước lẫn nhau (AutoSize cần biết size con,
            // con Dock=Top lại cần biết size cha) khiến WinForms co GroupBox gần về 0 và
            // Text tự xuống dòng từng ký tự. Dùng Size cố định — đủ chỗ cho nội dung bên trong.
            var tcpGrp = new GroupBox { Text = "TCP (giả lập bộ chuyển đổi Serial-to-Ethernet)", Padding = new Padding(8), Size = new Size(460, 190) };
            var tcpLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            _numTcpPort = new NumericUpDown { Width = 100, Minimum = 1, Maximum = 65535, Value = 23 };
            _btnTcpStartStop = new Button { Text = "▶ Bật TCP server", AutoSize = true };
            _lblTcpStatus = new Label { Text = "Đang dừng", AutoSize = true, ForeColor = Color.Gray };
            int r1 = 0;
            AddSingleRow(tcpLayout, ref r1, "Port:", _numTcpPort);
            AddSingleRow(tcpLayout, ref r1, "", _btnTcpStartStop);
            AddSingleRow(tcpLayout, ref r1, "Trạng thái:", _lblTcpStatus);
            var tcpHint = new Label
            {
                Text = "→ Trỏ ScaleConfig: ConnectionType=Tcp, IP=127.0.0.1, Port=cổng trên.",
                Dock = DockStyle.Top, AutoSize = false, Height = 34, ForeColor = Color.DimGray
            };
            // Controls cùng Dock=Top xếp chồng theo thứ tự NGƯỢC lúc Add (control add SAU CÙNG
            // nằm SÁT mép trên cùng) — add tcpLayout trước để nó lên trên, tcpHint sau để nằm dưới.
            tcpGrp.Controls.Add(tcpHint);
            tcpGrp.Controls.Add(tcpLayout);

            // ── COM panel ── (xem ghi chú "KHÔNG dùng AutoSize" ở tcpGrp phía trên)
            var comGrp = new GroupBox { Text = "COM (RS232 trực tiếp)", Padding = new Padding(8), Size = new Size(460, 235) };
            var comLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            _cboComPort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            _btnRefreshCom = new Button { Text = "⟳", Width = 32 };
            _cboBaud = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            _cboBaud.Items.AddRange(new object[] { 2400, 4800, 9600, 19200, 38400, 57600, 115200 });
            _cboBaud.SelectedItem = 9600;
            _btnComStartStop = new Button { Text = "▶ Bật COM writer", AutoSize = true };
            _lblComStatus = new Label { Text = "Đang dừng", AutoSize = true, ForeColor = Color.Gray };

            int r2 = 0;
            comLayout.Controls.Add(new Label { Text = "Cổng COM:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, r2);
            var comPortPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            comPortPanel.Controls.Add(_cboComPort);
            comPortPanel.Controls.Add(_btnRefreshCom);
            comLayout.Controls.Add(comPortPanel, 1, r2); r2++;
            comLayout.Controls.Add(new Label { Text = "Baud rate:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, r2);
            comLayout.Controls.Add(_cboBaud, 1, r2); r2++;
            comLayout.Controls.Add(new Label(), 0, r2);
            comLayout.Controls.Add(_btnComStartStop, 1, r2); r2++;
            comLayout.Controls.Add(new Label { Text = "Trạng thái:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, r2);
            comLayout.Controls.Add(_lblComStatus, 1, r2);

            var comHint = new Label
            {
                Text = "⚠ Cần cặp cổng COM ảo null-modem (vd. com0com: COM5↔COM6) hoặc 2 cổng " +
                       "COM thật nối cáp null-modem. Mở 1 đầu ở đây, trỏ ScaleConfig.ComPort (app test) sang đầu còn lại.",
                Dock = DockStyle.Top, AutoSize = false, Height = 48, ForeColor = Color.DimGray
            };
            comGrp.Controls.Add(comHint);
            comGrp.Controls.Add(comLayout);

            flow.Controls.Add(tcpGrp);
            flow.Controls.Add(comGrp);
            grp.Controls.Add(flow);

            RefreshComPorts();
            return grp;
        }

        private GroupBox BuildLogGroup()
        {
            var grp = new GroupBox { Text = "Log", Dock = DockStyle.Fill, Padding = new Padding(8) };
            _chkLogEachLine = new CheckBox { Text = "Ghi log từng dòng dữ liệu gửi ra (có thể chạy nhanh)", Dock = DockStyle.Top, AutoSize = true };
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font(FontFamily.GenericMonospace, 9)
            };
            grp.Controls.Add(_txtLog);
            grp.Controls.Add(_chkLogEachLine);
            return grp;
        }

        private static void AddRow(TableLayoutPanel grid, ref int row, string label1, Control ctrl1, string label2, Control ctrl2)
        {
            grid.RowCount = row + 1;
            grid.Controls.Add(new Label { Text = label1, AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            grid.Controls.Add(ctrl1, 1, row);
            grid.Controls.Add(new Label { Text = label2, AutoSize = true, Margin = new Padding(12, 8, 3, 3) }, 2, row);
            grid.Controls.Add(ctrl2, 3, row);
            row++;
        }

        private static void AddSingleRow(TableLayoutPanel grid, ref int row, string label, Control ctrl)
        {
            grid.RowCount = row + 1;
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, row);
            grid.Controls.Add(ctrl, 1, row);
            row++;
        }

        // ===================================================
        // WIRE EVENTS
        // ===================================================
        private void WireEvents()
        {
            _cboModel.SelectedIndexChanged += (_, _) => OnModelChanged();
            _numWeight.ValueChanged += (_, _) => ApplyConfig();
            _numNoise.ValueChanged += (_, _) => ApplyConfig();
            _cboUnit.SelectedIndexChanged += (_, _) => ApplyConfig();
            _cboFlag.SelectedIndexChanged += (_, _) => ApplyConfig();
            _numInterval.ValueChanged += (_, _) => ApplyConfig();
            _btnAutoRamp.Click += async (_, _) => await RunAutoRampAsync();

            _btnTcpStartStop.Click += (_, _) => ToggleTcp();
            _btnComStartStop.Click += (_, _) => ToggleCom();
            _btnRefreshCom.Click += (_, _) => RefreshComPorts();

            FormClosing += (_, _) =>
            {
                _engine.Dispose();
                _tcpServer.Dispose();
                _serialServer.Dispose();
            };
        }

        // ===================================================
        // MODEL / CONFIG
        // ===================================================
        private ScaleFormatDefinition CurrentFormat => (ScaleFormatDefinition)_cboModel.SelectedItem!;

        private void OnModelChanged()
        {
            var fmt = CurrentFormat;

            _numWeight.DecimalPlaces = fmt.DecimalPlaces;
            _numWeight.Increment = fmt.DecimalPlaces >= 3 ? 0.001m : 0.01m;
            _numWeight.Minimum = (decimal)fmt.MinWeight;
            _numWeight.Maximum = (decimal)fmt.MaxWeight;
            if (_numWeight.Value < _numWeight.Minimum) _numWeight.Value = _numWeight.Minimum;
            if (_numWeight.Value > _numWeight.Maximum) _numWeight.Value = _numWeight.Maximum;

            _cboUnit.Items.Clear();
            _cboUnit.Items.AddRange(fmt.Units.Cast<object>().ToArray());
            _cboUnit.Enabled = fmt.Units.Length > 0;
            if (fmt.Units.Length > 0) _cboUnit.SelectedIndex = 0;

            _cboFlag.Items.Clear();
            _cboFlag.Items.AddRange(fmt.FlagOptions.Cast<object>().ToArray());
            _cboFlag.Enabled = fmt.FlagOptions.Length > 0;
            if (fmt.FlagOptions.Length > 0) _cboFlag.SelectedIndex = 0;

            _lblNote.Text = $"Mẫu thật: {fmt.SampleHint}" + (fmt.Note != null ? $"\n{fmt.Note}" : "");

            ApplyConfig();
        }

        private void ApplyConfig()
        {
            var fmt = CurrentFormat;
            string? unit = _cboUnit.Enabled && _cboUnit.SelectedItem is string u ? u : null;
            string? flag = _cboFlag.Enabled && _cboFlag.SelectedItem is ScaleFlagOption f ? f.RawFlag : null;

            _engine.Configure(fmt, (double)_numWeight.Value, (double)_numNoise.Value, unit, flag, (int)_numInterval.Value);
        }

        private void OnLineGenerated(string line)
        {
            if (InvokeRequired) { BeginInvoke(() => OnLineGenerated(line)); return; }

            _txtPreview.Text = line;
            _tcpServer.Broadcast(line);
            _serialServer.Write(line);

            if (_chkLogEachLine.Checked)
                AppendLog($"→ {line}");
        }

        private async Task RunAutoRampAsync()
        {
            _btnAutoRamp.Enabled = false;
            try
            {
                var fmt = CurrentFormat;
                double target = (double)_numWeight.Value;
                double originalNoise = (double)_numNoise.Value;
                string? unit = _cboUnit.Enabled && _cboUnit.SelectedItem is string u ? u : null;
                string? flag = _cboFlag.Enabled && _cboFlag.SelectedItem is ScaleFlagOption f ? f.RawFlag : null;
                int intervalMs = (int)_numInterval.Value;

                AppendLog("[AUTO] Bắt đầu mô phỏng: về 0 → đặt vật → ổn định...");

                // 1) Về 0, giữ yên một nhịp — mô phỏng bàn cân trống trước khi đặt vật.
                _engine.Configure(fmt, 0, 0, unit, flag, intervalMs);
                await Task.Delay(400);

                // 2) Ramp lên target trong ~1.2s kèm nhiễu tạm thời (mô phỏng tay đặt vật/dao động cơ học).
                double rampNoise = Math.Max(originalNoise, Math.Abs(target) * 0.02 + 0.05);
                _engine.Configure(fmt, 0, rampNoise, unit, flag, intervalMs);
                const int steps = 12;
                for (int i = 1; i <= steps; i++)
                {
                    _engine.SetTargetWeight(target * i / steps);
                    await Task.Delay(100);
                }

                // 3) Ổn định — quay về đúng noise gốc user đã đặt (0 → driver sẽ báo Stable=true sau vài lần đọc).
                _engine.Configure(fmt, target, originalNoise, unit, flag, intervalMs);
                AppendLog("[AUTO] Hoàn tất — đang giữ ổn định ở giá trị target.");
            }
            finally
            {
                _btnAutoRamp.Enabled = true;
            }
        }

        // ===================================================
        // TCP
        // ===================================================
        private void ToggleTcp()
        {
            if (_tcpServer.IsRunning)
            {
                _tcpServer.Stop();
                _btnTcpStartStop.Text = "▶ Bật TCP server";
                _lblTcpStatus.Text = "Đang dừng";
                _lblTcpStatus.ForeColor = Color.Gray;
                _numTcpPort.Enabled = true;
            }
            else
            {
                try
                {
                    _tcpServer.Start((int)_numTcpPort.Value);
                    _btnTcpStartStop.Text = "■ Tắt TCP server";
                    _numTcpPort.Enabled = false;
                    UpdateTcpStatusLabel();
                }
                catch (Exception ex)
                {
                    AppendLog($"[TCP][ERROR] Không bật được server: {ex.Message}");
                }
            }
        }

        private void UpdateTcpStatusLabel()
        {
            if (!_tcpServer.IsRunning) return;
            _lblTcpStatus.Text = $"Đang chạy — {_tcpServer.ClientCount} client";
            _lblTcpStatus.ForeColor = Color.SeaGreen;
        }

        // ===================================================
        // COM
        // ===================================================
        private void RefreshComPorts()
        {
            string? previous = _cboComPort.SelectedItem as string;
            _cboComPort.Items.Clear();
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            _cboComPort.Items.AddRange(ports);
            if (ports.Length == 0)
            {
                AppendLog("[COM] Không tìm thấy cổng COM nào. Cần cài com0com (cổng ảo) hoặc cắm USB-to-Serial.");
                return;
            }
            _cboComPort.SelectedItem = previous != null && ports.Contains(previous) ? previous : ports[0];
        }

        private void ToggleCom()
        {
            if (_serialServer.IsRunning)
            {
                _serialServer.Stop();
                _btnComStartStop.Text = "▶ Bật COM writer";
                _lblComStatus.Text = "Đang dừng";
                _lblComStatus.ForeColor = Color.Gray;
                _cboComPort.Enabled = true;
                _cboBaud.Enabled = true;
            }
            else
            {
                if (_cboComPort.SelectedItem is not string comPort)
                {
                    AppendLog("[COM][ERROR] Chưa chọn cổng COM.");
                    return;
                }
                int baud = _cboBaud.SelectedItem is int b ? b : 9600;

                _serialServer.Start(comPort, baud);
                if (_serialServer.IsRunning)
                {
                    _btnComStartStop.Text = "■ Tắt COM writer";
                    _lblComStatus.Text = $"Đang chạy — {comPort} @ {baud}";
                    _lblComStatus.ForeColor = Color.SeaGreen;
                    _cboComPort.Enabled = false;
                    _cboBaud.Enabled = false;
                }
            }
        }

        // ===================================================
        // LOG
        // ===================================================
        private void AppendLog(string message)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendLog(message)); return; }

            _txtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");

            // Giới hạn log — tránh phình bộ nhớ khi chạy lâu / bật "log từng dòng".
            const int maxLines = 1000;
            if (_txtLog.Lines.Length > maxLines)
            {
                var keep = _txtLog.Lines.Skip(_txtLog.Lines.Length - maxLines).ToArray();
                _txtLog.Lines = keep;
                _txtLog.SelectionStart = _txtLog.Text.Length;
            }

            UpdateTcpStatusLabel();
        }
    }
}
