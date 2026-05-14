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

        /// <summary>Danh sách tất cả tên model hợp lệ.</summary>
        public static readonly string[] All =
        {
            DIGI, IND_KG, Vibra_SJ6200, Vibra_HAW30, SampleReading
        };
    }

    /// <summary>
    /// Cấu hình kết nối TCP/IP và xử lý dữ liệu cho Scale Driver.
    /// <para>
    /// Cân điện tử kết nối qua mạng LAN (Ethernet). Driver kết nối TCP
    /// rồi đọc liên tục theo chu kỳ <see cref="TimeScanMs"/>.
    /// Dữ liệu thô được parse bởi DLL model cân (Scale_DIGI.dll, v.v.).
    /// </para>
    /// </summary>
    public class ScaleConfig
    {
        /// <summary>
        /// Bật/tắt Scale Driver.
        /// Nếu false, sẽ không kết nối TCP.
        /// Mặc định: true.
        /// </summary>
        public bool Enable { get; set; } = true;

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
        /// Mặc định: 23.
        /// </summary>
        public int Port { get; set; } = 23;

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
        ///                      "Scale_Vibra_SJ6200", "Scale_SampleReading".
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
