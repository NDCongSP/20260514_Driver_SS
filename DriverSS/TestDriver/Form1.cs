using DevExpress.Utils;
using ScanAndScale.Driver;
using ScanAndScale.Helper;
using System.Diagnostics;
using System.Globalization;

namespace TestDriver
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;

            //barcodeButtonEdit1.Config = new BarcodeConfig()
            //{
            //    Enable = true,
            //    ReadOnly = false
            //};

            //rfidButtonEdit1.Config = new RfidConfig()
            //{
            //    Enable = true,
            //    ReadOnly = false,
            //    Rfid_AutoFindCom = true,
            //    Rfid_Caption = "Pongee",
            //    Rfid_Manufact = "Prolific",
            //    Rfid_Com = "COM1"
            //};

            //scaleButtonEdit1.Config = new ScaleConfig()
            //{
            //    Enable = true,
            //    ReadOnly = false,
            //    IP = "0.0.0.0",//    "192.168.0.15",
            //    Port = 23,
            //    ModelName = "Scale_DIGI"
            //};

        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            barcodeButtonEdit1.Config = GolobalTag.barcodeConfig;
            rfidButtonEdit1.Config = GolobalTag.rfidConfig;
            scaleButtonEdit1.Config = GolobalTag.scaleConfig;

            scaleButtonEdit2.Config = GolobalTag.scaleConfig;
            scaleButtonEdit2.EnableReadScale = true;
            scaleButtonEdit1.EnableReadScale = checkEdit1.Checked;

            barcodeButtonEdit1.DataValueChanged += BarcodeButtonEdit1_DataValueChanged;
            rfidButtonEdit1.DataValueChanged += RfidButtonEdit1_DataValueChanged;
            scaleButtonEdit1.DataValueChanged += ScaleButtonEdit1_DataValueChanged;

            checkEdit1.CheckedChanged += (s, o) =>
            {
                scaleButtonEdit1.EnableReadScale = checkEdit1.Checked;
            };

            _txtBagWeight.KeyDown += (s, o) =>
            {
                if (o.KeyCode == Keys.Enter)
                {
                    scaleButtonEdit1.BagWeight = double.TryParse(_txtBagWeight.Text, out double value) ? value : 0;
                }
            };

            _txtDecimalNum.KeyDown += (s, o) =>
            {
                if (o.KeyCode == Keys.Enter)
                {
                    //scaleButtonEdit1.DecimalNum = int.TryParse(_txtDecimalNum.Text, out int value) ? value : 0;
                }
            };

        }

        private void ScaleButtonEdit1_DataValueChanged(object? sender, DataValueChangedEventArgs e)
        {
            Debug.WriteLine($"Đã nhận được tín hiệu thay đổi Cân: '{e.NewValue.Value}'|{Convert.ToDouble(e.NewValue.Value?.ToString()) - scaleButtonEdit1.BagWeight}");
        }

        private void RfidButtonEdit1_DataValueChanged(object? sender, DataValueChangedEventArgs e)
        {
            MessageBox.Show($"Đã nhận được tín hiệu thay đổi RFID: '{e.NewValue.Value}'");
        }

        private void BarcodeButtonEdit1_DataValueChanged(object? sender, DataValueChangedEventArgs e)
        {
            MessageBox.Show($"Đã nhận được tín hiệu thay đổi barcode: '{e.NewValue.Value}'");
        }
    }
}
