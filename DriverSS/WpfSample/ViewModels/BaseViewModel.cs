// ============================================================
// File: ViewModels/BaseViewModel.cs
// Mục đích: Lớp base cho tất cả ViewModel trong MVVM pattern.
//           Implement INotifyPropertyChanged để WPF binding tự động
//           cập nhật UI khi property thay đổi.
// ============================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfSample.ViewModels
{
    /// <summary>
    /// Base class cho tất cả ViewModel.
    /// Cung cấp cơ chế INotifyPropertyChanged để WPF data binding.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        // -------------------------------------------------------
        // INotifyPropertyChanged implementation
        // WPF binding lắng nghe event này để tự cập nhật UI
        // -------------------------------------------------------
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Thông báo WPF rằng property có tên <paramref name="propertyName"/> đã thay đổi.
        /// WPF sẽ tự động cập nhật tất cả UI element đang bind vào property đó.
        /// </summary>
        /// <param name="propertyName">
        /// Tên property (tự động lấy từ caller nhờ [CallerMemberName]).
        /// Không cần truyền thủ công khi gọi từ setter của property.
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper: Gán giá trị mới cho field và tự động gọi OnPropertyChanged nếu giá trị thay đổi.
        /// Dùng trong setter của property để tránh lặp code.
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của field.</typeparam>
        /// <param name="field">Ref đến backing field.</param>
        /// <param name="value">Giá trị mới.</param>
        /// <param name="propertyName">Tên property (tự động lấy từ caller).</param>
        /// <returns>true nếu giá trị thực sự thay đổi, false nếu giá trị giống cũ.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            // Không làm gì nếu giá trị không thay đổi (tránh update UI không cần thiết)
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    // ============================================================
    // RelayCommand — Implement ICommand cho các nút bấm trong WPF
    // ============================================================

    /// <summary>
    /// Implementation đơn giản của ICommand cho WPF MVVM.
    /// Dùng để bind sự kiện nhấn nút (Button.Command) vào ViewModel.
    /// </summary>
    public class RelayCommand : ICommand
    {
        // Action sẽ thực thi khi command được gọi
        private readonly Action<object?> _execute;

        // Hàm kiểm tra command có thể thực thi không (có thể null = luôn true)
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// Khởi tạo RelayCommand.
        /// </summary>
        /// <param name="execute">Hành động khi nhấn nút.</param>
        /// <param name="canExecute">Điều kiện để nút được kích hoạt (null = luôn enabled).</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>Overload không có parameter.</summary>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute == null ? null : _ => canExecute())
        { }

        // WPF gọi hàm này để kiểm tra nút có được enable không
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        // WPF gọi hàm này khi nút được nhấn
        public void Execute(object? parameter) => _execute(parameter);

        // Kích hoạt WPF kiểm tra lại CanExecute
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>Bắt buộc WPF cập nhật lại trạng thái enabled của tất cả commands.</summary>
        public static void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}
