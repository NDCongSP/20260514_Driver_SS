using ScanAndScale.Helper;

namespace WinFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            barcodeButtonEdit1.Config = new ScanAndScale.Driver.BarcodeConfig() { Enable = true, ReadOnly = false };
            barcodeButtonEdit1.DataValueChanged += BarcodeButtonEdit1_DataValueChanged;
        }

        private void BarcodeButtonEdit1_DataValueChanged(object? sender, DataValueChangedEventArgs e)
        {
            MessageBox.Show($"Đã nhận được tín hiệu thay đổi barcode: '{e.NewValue.Value}'");
        }
    }
}
