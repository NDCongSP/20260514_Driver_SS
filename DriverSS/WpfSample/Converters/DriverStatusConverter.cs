// ============================================================
// File: Converters/DriverStatusConverter.cs
// Mục đích: Các ValueConverter để chuyển đổi DriverStatus
//           thành màu sắc hoặc text để hiển thị trong WPF UI.
//
// Dùng trong XAML:
//   xmlns:conv="clr-namespace:WpfSample.Converters"
//   <conv:DriverStatusToColorConverter x:Key="StatusToColor"/>
//   <Ellipse Fill="{Binding BarcodeStatus, Converter={StaticResource StatusToColor}}"/>
// ============================================================

using ScanAndScale.Core.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfSample.Converters
{
    // ============================================================
    // CONVERTER 1: DriverStatus → Màu sắc (đèn LED trạng thái)
    // ============================================================

    /// <summary>
    /// Chuyển DriverStatus thành màu sắc để hiển thị đèn LED trạng thái.
    /// Connected = Xanh lá, Disconnected = Đỏ, Reconnecting = Vàng, Unknown = Xám
    /// </summary>
    [ValueConversion(typeof(DriverStatus), typeof(Brush))]
    public class DriverStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DriverStatus status)
            {
                return status switch
                {
                    DriverStatus.Connected => new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45)),    // Xanh lá
                    DriverStatus.Disconnected => new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45)),  // Đỏ
                    DriverStatus.Reconnecting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),  // Vàng
                    _ => new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D))                          // Xám
                };
            }
            return Brushes.Gray;
        }

        // Không cần ConvertBack vì chỉ dùng 1 chiều
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ============================================================
    // CONVERTER 2: bool (Stable/Tare) → Màu nền TextBox cân
    // ============================================================

    /// <summary>
    /// Chuyển trạng thái Stable (bool) thành màu chữ cho TextBox hiển thị cân.
    /// Stable = Xanh, Unstable = Đỏ
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class StableToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Stable = true → màu xanh; false → màu đỏ
            if (value is bool isStable)
                return isStable ? Brushes.DarkGreen : Brushes.Red;
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ============================================================
    // CONVERTER 3: bool (Tare) → Màu nền
    // ============================================================

    /// <summary>
    /// Chuyển trạng thái Tare thành màu nền.
    /// Tare = true → nền hồng nhạt; false → transparent
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class TareToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTare)
                return isTare
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD0, 0xD0)) // Hồng nhạt khi Tare
                    : Brushes.White;
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ============================================================
    // CONVERTER 4: DriverStatus → Visibility (hiện/ẩn element)
    // ============================================================

    /// <summary>
    /// Chuyển DriverStatus thành Visibility.
    /// Dùng để ẩn/hiện các element theo trạng thái kết nối.
    /// Parameter = "Connected" → chỉ hiện khi Connected
    /// Parameter = "Disconnected" → chỉ hiện khi Disconnected
    /// </summary>
    [ValueConversion(typeof(DriverStatus), typeof(Visibility))]
    public class DriverStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DriverStatus status)
            {
                var targetStatus = parameter?.ToString() switch
                {
                    "Connected" => DriverStatus.Connected,
                    "Disconnected" => DriverStatus.Disconnected,
                    _ => DriverStatus.Connected
                };

                return status == targetStatus ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ============================================================
    // CONVERTER 5: InverseBool → Visibility
    // ============================================================

    /// <summary>
    /// Chuyển bool đảo ngược thành Visibility.
    /// true → Collapsed, false → Visible (ngược lại với mặc định WPF).
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
