// ============================================================
// File: Models/BarcodeConfig.cs
// Mục đích: Cấu hình cho Barcode Driver (Zebra Scanner SDK)
// ============================================================

using System;

namespace ScanAndScale.Core.Models
{
    /// <summary>
    /// Cấu hình kết nối và hành vi của Barcode Driver (Zebra CoreScanner SDK).
    /// <para>
    /// Lưu ý: Zebra CoreScanner SDK phải được cài đặt trên máy.
    /// Sau đó copy file Interop.CoreScanner.dll vào thư mục lib\ của project thư viện.
    /// </para>
    /// </summary>
    public class BarcodeConfig
    {
        /// <summary>
        /// Bật/tắt Barcode Driver.
        /// Nếu false, driver sẽ không khởi tạo và không gọi API Zebra.
        /// Mặc định: true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Chỉ đọc — nếu true, giá trị barcode sẽ không cho phép chỉnh sửa
        /// (chỉ dùng khi tích hợp vào UI control).
        /// Mặc định: false.
        /// </summary>
        public bool ReadOnly { get; set; } = false;

        /// <summary>
        /// Danh sách Scanner ID cần lắng nghe.
        /// Null hoặc rỗng = lắng nghe tất cả scanner đang kết nối.
        /// </summary>
        public int[]? ScannerIdFilter { get; set; } = null;
    }
}
