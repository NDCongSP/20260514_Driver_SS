// ============================================================
// File: Helpers/DriverHelper.cs
// Mục đích: Các hàm tiện ích dùng chung cho tất cả drivers.
//           KHÔNG có dependency vào WinForms hoặc WPF.
// ============================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ScanAndScale.Core.Helpers
{
    /// <summary>
    /// Tập hợp các hàm tiện ích dùng chung cho các driver.
    /// Tất cả đều là static methods — không cần tạo instance.
    /// </summary>
    public static class DriverHelper
    {
        // -------------------------------------------------------
        // Chuyển đổi kiểu dữ liệu an toàn (không throw exception)
        // -------------------------------------------------------

        /// <summary>
        /// Chuyển đổi một object sang double một cách an toàn.
        /// Trả về 0 nếu không thể chuyển đổi (null, DBNull, chữ cái, v.v.).
        /// </summary>
        /// <param name="value">Giá trị cần chuyển đổi.</param>
        /// <returns>Giá trị double, hoặc 0 nếu không hợp lệ.</returns>
        public static double ToDouble(object? value)
        {
            // Trường hợp null hoặc DBNull — trả về 0
            if (value == null || value == DBNull.Value)
                return 0;

            // Nếu đã là double rồi thì trả về thẳng
            if (value is double d)
                return d;

            // Thử parse từ string
            var str = value.ToString();
            if (string.IsNullOrWhiteSpace(str))
                return 0;

            // Dùng CultureInfo en-US để xử lý dấu thập phân "." thay vì ","
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            // Thử kiểu dữ liệu chứa chữ
            if (str.Any(char.IsLetter))
                return 0;

            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        // -------------------------------------------------------
        // Logging an toàn (không làm crash ứng dụng)
        // -------------------------------------------------------

        /// <summary>
        /// Ghi log lỗi ra Debug Output (không throw exception).
        /// </summary>
        /// <param name="ex">Exception cần ghi log.</param>
        /// <param name="context">Tên ngữ cảnh (tên driver, method, v.v.).</param>
        public static void LogError(Exception ex, string context = "")
        {
            var prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            Debug.WriteLine($"{prefix}ERROR: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
        }

        /// <summary>
        /// Ghi thông tin ra Debug Output.
        /// </summary>
        public static void LogInfo(string message, string context = "")
        {
            var prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            Debug.WriteLine($"{prefix}{message}");
        }

        // -------------------------------------------------------
        // Chuyển đổi đơn vị cân
        // -------------------------------------------------------

        /// <summary>
        /// Chuyển đổi giá trị cân từ KG sang đơn vị mong muốn.
        /// </summary>
        /// <param name="valueKg">Giá trị tính bằng KG.</param>
        /// <param name="targetUnit">Đơn vị đích: "KG", "G", "TON".</param>
        /// <returns>Giá trị đã quy đổi.</returns>
        public static double ConvertScaleUnit(double valueKg, string targetUnit)
        {
            return targetUnit.ToUpperInvariant() switch
            {
                "G" or "GRAM" => valueKg * 1000.0,
                "TON" => valueKg / 1000.0,
                _ => valueKg  // Mặc định giữ nguyên KG
            };
        }
    }
}
