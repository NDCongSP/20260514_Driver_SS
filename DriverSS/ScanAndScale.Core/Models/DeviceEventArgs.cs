// ============================================================
// File: Models/DeviceEventArgs.cs
// Mục đích: Định nghĩa các lớp EventArgs dùng để thông báo
//           khi dữ liệu từ thiết bị thay đổi.
//
// QUAN TRỌNG: Sự kiện DataValueChanged có thể được gọi từ
// background thread (không phải UI thread). Khi xử lý event
// trong WinForms hoặc WPF, cần dispatch về UI thread:
//
//   WPF:      Application.Current.Dispatcher.Invoke(() => { ... });
//   WinForms: control.Invoke(() => { ... });
// ============================================================

using System;

namespace ScanAndScale.Core.Models
{
    /// <summary>
    /// EventArgs thông báo khi giá trị DataValue của thiết bị thay đổi.
    /// Chứa cả giá trị cũ và giá trị mới để so sánh nếu cần.
    /// </summary>
    public class DataValueChangedEventArgs : EventArgs
    {
        // -------------------------------------------------------
        // Giá trị trước khi thay đổi (để so sánh hoặc hoàn tác)
        // -------------------------------------------------------
        /// <summary>Giá trị DataValue TRƯỚC khi thay đổi.</summary>
        public DataValue OldValue { get; }

        // -------------------------------------------------------
        // Giá trị mới nhất vừa nhận được từ thiết bị
        // -------------------------------------------------------
        /// <summary>Giá trị DataValue MỚI vừa nhận được.</summary>
        public DataValue NewValue { get; }

        /// <summary>
        /// Khởi tạo EventArgs với giá trị cũ và mới.
        /// </summary>
        public DataValueChangedEventArgs(DataValue oldValue, DataValue newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// EventArgs thông báo khi trạng thái kết nối thiết bị thay đổi.
    /// </summary>
    public class DriverStatusChangedEventArgs : EventArgs
    {
        /// <summary>Trạng thái cũ.</summary>
        public DriverStatus OldStatus { get; }

        /// <summary>Trạng thái mới.</summary>
        public DriverStatus NewStatus { get; }

        /// <summary>
        /// Thông báo lỗi nếu trạng thái thay đổi do lỗi (ví dụ: mất kết nối do exception).
        /// Null nếu thay đổi bình thường.
        /// </summary>
        public string? ErrorMessage { get; }

        public DriverStatusChangedEventArgs(DriverStatus oldStatus, DriverStatus newStatus, string? errorMessage = null)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            ErrorMessage = errorMessage;
        }
    }
}
