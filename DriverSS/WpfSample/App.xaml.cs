// ============================================================
// File: App.xaml.cs
// Mục đích: Code-behind của App.xaml
//           Xử lý các sự kiện vòng đời ứng dụng (startup/exit).
// ============================================================

using System.Windows;

namespace WpfSample
{
    /// <summary>
    /// Code-behind của App.xaml.
    /// Xử lý startup và shutdown của toàn bộ ứng dụng.
    /// </summary>
    public partial class App : Application
    {
        // -------------------------------------------------------
        // OnStartup — Chạy trước khi MainWindow hiển thị
        // -------------------------------------------------------
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Xử lý exception không bắt được trên UI thread
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(
                    $"Lỗi không xử lý được:\n{ex.Exception.Message}",
                    "Lỗi ứng dụng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                ex.Handled = true; // Không crash app
            };

            // Xử lý exception không bắt được trên background thread
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                System.Diagnostics.Debug.WriteLine($"UnobservedTaskException: {ex.Exception}");
                ex.SetObserved();
            };
        }

        // -------------------------------------------------------
        // OnExit — Chạy khi ứng dụng thoát
        // -------------------------------------------------------
        protected override void OnExit(ExitEventArgs e)
        {
            // Tất cả các driver đã được dispose trong MainWindow.OnClosed
            // Chỉ cần cleanup ở đây nếu có resource toàn cục
            base.OnExit(e);
        }
    }
}
