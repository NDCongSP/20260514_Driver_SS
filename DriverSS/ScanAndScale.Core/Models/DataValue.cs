// ============================================================
// File: Models/DataValue.cs
// Mục đích: Lớp đóng gói giá trị đọc được từ thiết bị
//           kèm theo trạng thái kết nối tại thời điểm đó.
// ============================================================

using System;

namespace ScanAndScale.Core.Models
{
    /// <summary>
    /// Đóng gói một giá trị đọc được từ thiết bị cùng trạng thái kết nối.
    /// <para>
    /// Ví dụ:<br/>
    ///   - Barcode: Value = "ABC123", Status = Connected<br/>
    ///   - RFID: Value = "12345", Status = Connected<br/>
    ///   - Scale: Value = 3.14 (kg), Status = Connected<br/>
    ///   - Khi mất kết nối: Value = null, Status = Disconnected
    /// </para>
    /// </summary>
    public class DataValue
    {
        // -------------------------------------------------------
        // Trạng thái thiết bị tại thời điểm đọc dữ liệu
        // -------------------------------------------------------
        /// <summary>Trạng thái kết nối của thiết bị.</summary>
        public DriverStatus DriverStatus { get; set; }

        // -------------------------------------------------------
        // Giá trị đọc được (có thể là string, double, object...)
        // null nếu thiết bị mất kết nối
        // -------------------------------------------------------
        /// <summary>
        /// Giá trị thực tế đọc được từ thiết bị.
        /// Có thể là: string (Barcode/RFID), double (Scale), hoặc null khi mất kết nối.
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Khởi tạo DataValue với trạng thái và giá trị cụ thể.
        /// </summary>
        /// <param name="status">Trạng thái kết nối thiết bị.</param>
        /// <param name="value">Giá trị đọc được (null nếu không có dữ liệu).</param>
        public DataValue(DriverStatus status, object? value)
        {
            DriverStatus = status;
            Value = value;
        }

        /// <summary>
        /// Kiểm tra xem dữ liệu có hợp lệ không (thiết bị kết nối và có giá trị).
        /// </summary>
        // Lưu ý: DriverStatus (property) so sánh với Models.DriverStatus.Connected (enum value)
        public bool IsValid => DriverStatus == Models.DriverStatus.Connected && Value != null;

        /// <inheritdoc/>
        public override string ToString()
            => $"[{DriverStatus}] {Value}";
    }
}
