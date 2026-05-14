namespace ScanAndScale.Driver
{
    public class BarcodeConfig
    {
        public BarcodeConfig()
        {
        }
        public bool ReadOnly { get; set; } = false;
        public bool Enable { get; set; } = true;

    }
    public class RfidConfig
    {
        public RfidConfig()
        {
        }
        public string Rfid_Com { get; set; } = "COM1";
        public bool Rfid_AutoFindCom { get; set; } = true;

        //"Pongee"
        public string Rfid_Caption { get; set; } = "Pongee";
        //"Prolific"
        public string Rfid_Manufact { get; set; } = "Prolific";
        public bool ReadOnly { get; set; } = false;
        public bool Enable { get; set; } = true;
    }
    public class ScaleConfig
    {

        public bool? CheckTare { get; set; } = false;

        public bool? CheckStable { get; set; } = false;
        public bool? Enable { get; set; } = true;
        public bool? ReadOnly { get; set; } = true;

        public string ModelName { get; set; } = "DIGI";
        public string IP { get; set; } = "0.0.0.0";


        public int Port { get; set; } = 23;
        public int TimeScan { get; set; } = 400;
        public double CalibZero { get; set; } = 0.0;
        public double CalibGain { get; set; } = 1.0;
        public double LastWeight { get; set; }
        public int DecimalNum { get; set; } = 6;
    }

}