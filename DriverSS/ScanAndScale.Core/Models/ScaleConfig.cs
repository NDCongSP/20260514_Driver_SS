// ============================================================
// File: Models/ScaleConfig.cs
// Mục đích: Cấu hình cho Scale Driver (đọc cân qua TCP/IP)
// ============================================================

namespace ScanAndScale.Core.Models
{
    /// <summary>
    /// Các tên model cân được hỗ trợ — dùng thay cho chuỗi string
    /// để tránh lỗi typo khi cấu hình <see cref="ScaleConfig.ModelName"/>.
    /// <para>
    /// Mỗi tên tương ứng với một file DLL parser (ví dụ: Scale_DIGI.dll).
    /// DLL đó phải tồn tại cùng thư mục với file .exe của ứng dụng.
    /// </para>
    /// </summary>
    public static class ScaleModelNames
    {
        /// <summary>Cân DIGI — format: "3.08@="</summary>
        public const string DIGI = "Scale_DIGI";

        /// <summary>Cân IND đơn vị KG — format: "=0004.62(kg)"</summary>
        public const string IND_KG = "Scale_IND_KG";

        /// <summary>Cân Vibra SJ-6200</summary>
        public const string Vibra_SJ6200 = "Scale_Vibra_SJ6200";

        /// <summary>Cân Vibra HAW-30</summary>
        public const string Vibra_HAW30 = "Scale_Vibra_HAW30";

        /// <summary>Cân mẫu / test — format Vibra generic</summary>
        public const string SampleReading = "Scale_SampleReading";

        /// <summary>Cân Shimadzu TX4202L (dòng UniBloc, max 4200g) — format: "48.08g"</summary>
        public const string Shimadzu_TX4202L = "Scale_Shimadzu_TX4202L";

        /// <summary>Danh sách tất cả tên model hợp lệ.</summary>
        public static readonly string[] All =
        {
            DIGI, IND_KG, Vibra_SJ6200, Vibra_HAW30, SampleReading, Shimadzu_TX4202L
        };
    }

    /// <summary>
    /// Kiểu kết nối vật lý tới cân điện tử.
    /// </summary>
    public enum ScaleConnectionType
    {
        /// <summary>
        /// Kết nối qua TCP/IP — hoặc cân có cổng Ethernet trực tiếp, hoặc (phổ biến
        /// hơn) cân xuất RS232 qua bộ chuyển đổi Serial-to-Ethernet. Mặc định — giữ
        /// nguyên hành vi cũ, KHÔNG đổi cấu hình nào đang chạy ngoài thực tế.
        /// </summary>
        Tcp,

        /// <summary>
        /// Kết nối trực tiếp qua cổng COM (RS232 cắm thẳng vào máy tính, hoặc
        /// USB-to-Serial), không qua bộ chuyển đổi TCP. Dùng <see cref="ScaleConfig.ComPort"/>
        /// và <see cref="ScaleConfig.BaudRate"/>.
        /// </summary>
        Com
    }

    /// <summary>
    /// Cấu hình kết nối (TCP/IP hoặc COM) và xử lý dữ liệu cho Scale Driver.
    /// <para>
    /// Cân điện tử có thể kết nối theo 2 cách, chọn qua <see cref="ConnectionType"/>:
    /// (1) TCP/IP — qua mạng LAN hoặc bộ chuyển đổi RS232-to-TCP (IP/Port);
    /// (2) COM — cắm trực tiếp cổng Serial/USB-to-Serial (ComPort/BaudRate).
    /// Dù kết nối kiểu nào, driver đều đọc liên tục theo chu kỳ <see cref="TimeScanMs"/>
    /// và parse dữ liệu thô bằng DLL model cân (Scale_DIGI.dll, v.v.) — không phụ
    /// thuộc vào kiểu kết nối.
    /// </para>
    /// </summary>
    public class ScaleConfig
    {
        /// <summary>
        /// Bật/tắt Scale Driver.
        /// Nếu false, sẽ không kết nối (dù TCP hay COM).
        /// Mặc định: true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Kiểu kết nối tới cân: TCP/IP hay COM trực tiếp.
        /// Mặc định: <see cref="ScaleConnectionType.Tcp"/> — giữ tương thích ngược,
        /// các cấu hình cũ (chỉ set IP/Port) không cần đổi gì vẫn chạy như trước.
        /// </summary>
        public ScaleConnectionType ConnectionType { get; set; } = ScaleConnectionType.Tcp;

        /// <summary>
        /// Chỉ đọc — nếu true không cho phép nhập tay trên UI.
        /// Mặc định: true (cân không cho nhập tay).
        /// </summary>
        public bool ReadOnly { get; set; } = true;

        /// <summary>
        /// Địa chỉ IP của cân điện tử.
        /// Ví dụ: "192.168.80.237".
        /// Mặc định: "0.0.0.0" (chưa cấu hình).
        /// </summary>
        public string IP { get; set; } = "0.0.0.0";

        /// <summary>
        /// Cổng TCP của cân. Hầu hết cân dùng Telnet port 23.
        /// Chỉ dùng khi <see cref="ConnectionType"/> = Tcp.
        /// Mặc định: 23.
        /// </summary>
        public int Port { get; set; } = 23;

        /// <summary>
        /// Tên cổng COM (ví dụ: "COM3") khi cân cắm trực tiếp qua Serial/USB-to-Serial.
        /// Chỉ dùng khi <see cref="ConnectionType"/> = Com.
        /// Mặc định: "COM1".
        /// </summary>
        public string ComPort { get; set; } = "COM1";

        /// <summary>
        /// Baud rate cổng COM — phải khớp với cấu hình vật lý của cân (thường in trên
        /// nhãn máy hoặc tài liệu kỹ thuật, ví dụ 9600, 4800, 2400).
        /// Chỉ dùng khi <see cref="ConnectionType"/> = Com.
        /// Mặc định: 9600.
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// Chu kỳ đọc dữ liệu từ cân (milliseconds).
        /// Giá trị nhỏ = cập nhật nhanh hơn nhưng tốn tài nguyên hơn.
        /// Mặc định: 400ms (2.5 lần/giây).
        /// </summary>
        public int TimeScanMs { get; set; } = 400;

        /// <summary>
        /// Hiệu chỉnh zero — cộng thêm vào kết quả đọc được.
        /// Dùng để bù sai số offset của cân.
        /// Công thức: giá trị thực = (đọc + CalibZero) * CalibGain
        /// Mặc định: 0.0 (không hiệu chỉnh).
        /// </summary>
        public double CalibZero { get; set; } = 0.0;

        /// <summary>
        /// Hệ số hiệu chỉnh gain — nhân với kết quả sau khi cộng CalibZero.
        /// Mặc định: 1.0 (không hiệu chỉnh).
        /// </summary>
        public double CalibGain { get; set; } = 1.0;

        /// <summary>
        /// Số chữ số thập phân khi làm tròn kết quả.
        /// Mặc định: 3 (ví dụ: 3.142).
        /// </summary>
        public int DecimalNum { get; set; } = 3;

        /// <summary>
        /// Tên model cân — tương ứng với tên DLL parser.
        /// Các giá trị hợp lệ: "Scale_DIGI", "Scale_IND_KG", "Scale_Vibra_HAW30",
        ///                      "Scale_Vibra_SJ6200", "Scale_SampleReading",
        ///                      "Scale_Shimadzu_TX4202L".
        /// DLL tương ứng (ví dụ Scale_DIGI.dll) phải nằm trong thư mục chạy của ứng dụng.
        /// Mặc định: "Scale_DIGI".
        /// </summary>
        public string ModelName { get; set; } = "Scale_DIGI";

        /// <summary>
        /// Kiểm tra trạng thái ổn định (Stable) của cân trước khi báo giá trị.
        /// Nếu true, sự kiện DataValueChanged chỉ kích hoạt khi cân báo stable.
        /// Mặc định: false (báo dữ liệu liên tục dù cân chưa ổn định).
        /// </summary>
        public bool CheckStable { get; set; } = false;

        /// <summary>
        /// Kiểm tra trạng thái Tare (bì) của cân.
        /// Nếu true và cân báo đang tare, thông tin Tare sẽ được đánh dấu trong DataValue.
        /// Mặc định: false.
        /// </summary>
        public bool CheckTare { get; set; } = false;
    }
}
