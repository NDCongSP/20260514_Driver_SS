// ============================================================
// File: Models/RfidConfig.cs
// Mục đích: Cấu hình cho RFID Driver (đọc qua SerialPort)
// ============================================================

namespace ScanAndScale.Core.Models
{
    /// <summary>
    /// Cấu hình kết nối SerialPort cho RFID Reader.
    /// <para>
    /// Thiết bị RFID kết nối qua cổng COM (USB-to-Serial).
    /// Có thể chỉ định cổng COM cụ thể hoặc bật AutoFind để tự tìm.
    /// </para>
    /// </summary>
    public class RfidConfig
    {
        /// <summary>
        /// Bật/tắt RFID Driver.
        /// Nếu false, driver sẽ không mở cổng COM.
        /// Mặc định: true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Chỉ đọc — nếu true không cho phép chỉnh sửa giá trị trên UI.
        /// Mặc định: false.
        /// </summary>
        public bool ReadOnly { get; set; } = false;

        /// <summary>
        /// Tên cổng COM cụ thể (ví dụ: "COM3", "COM5").
        /// Chỉ dùng khi <see cref="AutoFindCom"/> = false.
        /// Mặc định: "COM1".
        /// </summary>
        public string ComPort { get; set; } = "COM1";

        /// <summary>
        /// Tự động tìm cổng COM của thiết bị RFID dựa vào tên và nhà sản xuất.
        /// Nếu true, hệ thống sẽ duyệt tất cả COM ports và tìm đúng thiết bị.
        /// Mặc định: true.
        /// </summary>
        public bool AutoFindCom { get; set; } = true;

        /// <summary>
        /// Tên hiển thị (Caption) của thiết bị USB-to-Serial trong Device Manager.
        /// Dùng để tự tìm cổng COM khi <see cref="AutoFindCom"/> = true.
        /// Ví dụ: "Pongee" (hay dùng ở nhà máy).
        /// </summary>
        public string DeviceCaption { get; set; } = "Pongee";

        /// <summary>
        /// Tên nhà sản xuất chip USB-to-Serial trong Device Manager.
        /// Dùng để tự tìm cổng COM khi <see cref="AutoFindCom"/> = true.
        /// Ví dụ: "Prolific" (chip PL2303 phổ biến).
        /// </summary>
        public string DeviceManufacturer { get; set; } = "Prolific";

        /// <summary>
        /// Baud rate của cổng COM. Mặc định: 9600 (theo chuẩn thiết bị RFID).
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// Pattern (Regex) để trích xuất mã RFID từ chuỗi dữ liệu nhận được.
        /// Mặc định: lấy 5 chữ số cuối loại bỏ leading zeros.
        /// </summary>
        public string DataPattern { get; set; } = @"\b0*(\d{5})\b";
    }
}
