// ============================================================
// File: Models/DriverStatus.cs
// Mục đích: Định nghĩa enum trạng thái kết nối thiết bị
// Dùng chung cho cả 3 loại: Barcode, RFID, Scale
// ============================================================

namespace ScanAndScale.Core.Models
{
    /// <summary>
    /// Trạng thái kết nối của thiết bị đọc dữ liệu.
    /// </summary>
    public enum DriverStatus
    {
        /// <summary>
        /// Chưa khởi tạo, chưa biết trạng thái.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Đang kết nối và nhận dữ liệu bình thường.
        /// </summary>
        Connected = 1,

        /// <summary>
        /// Mất kết nối (cổng đóng, không ping được, v.v.).
        /// </summary>
        Disconnected = 2,

        /// <summary>
        /// Đang thử kết nối lại sau khi mất kết nối.
        /// </summary>
        Reconnecting = 3,
    }
}
