// ============================================================
// File: MainWindow.xaml.cs
// Mục đích: Code-behind của MainWindow.xaml
//
// Vai trò trong MVVM:
//   • Code-behind chỉ làm 2 việc:
//     1. Tạo ViewModel và gán vào DataContext
//     2. Gọi Init/Dispose khi window Load/Close
//   • Toàn bộ logic xử lý driver nằm trong MainViewModel
//   • UI cập nhật tự động qua WPF data binding
// ============================================================

using WpfSample.ViewModels;
using System.Windows;

namespace WpfSample
{
    /// <summary>
    /// Code-behind của MainWindow.
    /// <para>
    /// Theo pattern MVVM, code-behind nên càng ngắn càng tốt.
    /// Logic nghiệp vụ và xử lý driver đều nằm trong <see cref="MainViewModel"/>.
    /// </para>
    /// </summary>
    public partial class MainWindow : Window
    {
        // -------------------------------------------------------
        // ViewModel — Toàn bộ data và logic nằm ở đây
        // -------------------------------------------------------
        private readonly MainViewModel _viewModel;

        // -------------------------------------------------------
        // CONSTRUCTOR — Chạy khi Window được tạo lần đầu
        // -------------------------------------------------------
        public MainWindow()
        {
            // Bước 1: Khởi tạo các component được định nghĩa trong XAML
            // (Các Button, TextBox, GroupBox, v.v.)
            InitializeComponent();

            // Bước 2: Tạo ViewModel instance
            _viewModel = new MainViewModel();

            // Bước 3: Gán ViewModel làm DataContext của Window
            // Sau bước này, tất cả {Binding ...} trong XAML sẽ nhìn vào _viewModel
            DataContext = _viewModel;
        }

        // -------------------------------------------------------
        // LOADED — Chạy sau khi Window hiển thị hoàn toàn
        // -------------------------------------------------------
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hiển thị hướng dẫn khởi tạo
            // (Tự động kết nối hoặc chờ user nhấn nút — tùy yêu cầu)

            // OPTION A: Tự động kết nối ngay khi mở app
            // _viewModel.InitializeAllDrivers();

            // OPTION B: Chờ user nhấn nút "Kết nối tất cả" (Command đã bind trong XAML)
            // → Không cần gọi gì ở đây

            // Hiện thông báo cho user biết cần nhấn nút
            System.Diagnostics.Debug.WriteLine("MainWindow loaded. Nhấn 'Kết nối tất cả' để bắt đầu.");
        }

        // -------------------------------------------------------
        // CLOSED — Chạy khi user đóng Window (X hoặc Alt+F4)
        // -------------------------------------------------------
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // QUAN TRỌNG: Phải dispose drivers khi đóng app
            // Nếu không, serial port có thể bị lock, TCP connection không đóng
            _viewModel.DisposeAllDrivers();

            System.Diagnostics.Debug.WriteLine("MainWindow closed. Tất cả drivers đã được dispose.");
        }
    }
}
