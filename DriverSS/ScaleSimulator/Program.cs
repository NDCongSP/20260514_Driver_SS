// ============================================================
// File: Program.cs
// Mục đích: Entry point của ứng dụng ScaleSimulator (WinForms).
// ============================================================

namespace ScaleSimulator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
