using Microsoft.Win32;
using ScanAndScale.Helper;
using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace ScanAndScale.Driver
{
    public class RFIDMonitoredItem
    {

        public static int NumSlave { get; set; } = 0;
        public string ComPort { get; set; } = "COM3";

        private SerialPort _serialPort;

        #region Singleton Pattern

        private static RFIDMonitoredItem _instance;

        public static RFIDMonitoredItem Instance
        {
            private set => _instance = value;
            get
            {
                if (_instance == null)
                {
                    _instance = new RFIDMonitoredItem();
                }
                return _instance;
            }
        }


        #endregion


        #region tao event cho moi quet the
        private DriverStatus _Status;

        public DriverStatus Status
        {
            get { return _Status; }
            private set
            {
                if (!_Status.Equals(value))
                {
                    _Status = value;
                    DataValue = new DataValue(_Status, _Value);
                }
            }
        }

        private string _Value;

        public string Value
        {
            get { return _Value; }
            private set
            {
                _Value = value;
                DataValue = new DataValue(_Status, _Value);
            }
        }

        private DataValue _datavalue = new DataValue(DriverStatus.Unknown, null);
        private object value;
        private string rfid_Com;
        private object rfid_AutoFindCom;

        public DataValue DataValue
        {
            get => _datavalue;
            set
            {
                var oldValue = _datavalue;
                _datavalue = value;
                OnDataChanged(oldValue, value);
            }
        }



        //private static event EventHandler<DataValueChangedEventArgs> _DataValueChangedPublic;
        //public static event EventHandler<DataValueChangedEventArgs> DataValueChangedPublic
        //{
        //    add
        //    {
        //        _DataValueChangedPublic += value;
        //    }
        //    remove
        //    {
        //        _DataValueChangedPublic -= value;
        //    }
        //}
        //public static void OnDataChangedPulic(DataValue Value)
        //{
        //    _DataValueChangedPublic?.Invoke(null, new DataValueChangedEventArgs(Value, Value));
        //}



        private event EventHandler<DataValueChangedEventArgs> _dataValueChanged;
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add
            {
                _dataValueChanged += value;
            }
            remove
            {
                _dataValueChanged -= value;
            }
        }

        void OnDataChanged(DataValue oldValue, DataValue value)
        {
            //Debug.WriteLine($"OnDataChanged {value.Value}");

            _dataValueChanged?.Invoke(this, new DataValueChangedEventArgs(oldValue, value));
        }
        #endregion

        public RFIDMonitoredItem()
        {
        }
        //Dùng để truyền thông số vào
        private void RFIDMonitoredItem_DataValueChangedPublic(object? sender, DataValueChangedEventArgs e)
        {
            DataValue = e.NewValue;
        }

        public void SerialportOpen(bool Enable, string com, bool autofind, bool reconect = false)
        {
            try
            {
                if (!reconect)
                {
                    NumSlave = NumSlave + 1;
                }

                if (NumSlave > 1) { return; }

                if (!Enable)
                    return;
                if (_serialPort != null && _serialPort.IsOpen)
                    return;

                if (autofind)
                {
                    com = AutoFindSerialPort();
                }
                if (string.IsNullOrEmpty(com))
                {
                    return;
                }
                ComPort = com;
                _serialPort = new System.IO.Ports.SerialPort(com, 9600, Parity.None, 8, StopBits.One);

                if (!_serialPort.IsOpen)
                {
                    _serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);

                    _serialPort.Open();
                    Debug.WriteLine("Ket noi RFID thanh cong.");
                }
                else
                {
                    Debug.WriteLine("COM da dc mo.");
                }
                Status = DriverStatus.Connected;
            }
            catch (Exception ex)
            {
                Status = DriverStatus.Disconnected;
            }
        }


        public void SerialClose()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
                Debug.WriteLine("Ngat ket noi RFID thanh cong.");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"Bat dau doc RFID");
                string dataRCV = _serialPort.ReadLine();
                if (string.IsNullOrEmpty(dataRCV))
                {
                    return;
                }

                //string input = "\u0003\u00020000008991\r";

                // Define a regular expression pattern to match the number with leading zeros
                string pattern = @"\b0*(\d{5})\b";

                // Use Regex.Match to find the first occurrence of the pattern in the input string
                Match match = Regex.Match(dataRCV, pattern);

                // Check if a match was found
                if (match.Success)
                {
                    Value = match.Groups[1].Value;
                }
                else
                {
                    Value = "Scan again";
                }

                Debug.WriteLine($"Doc RFID thanh cong: {_Status}");
                Status = DriverStatus.Connected;

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Status = DriverStatus.Disconnected;
                //  SerialClose();
                // SerialportOpen();
            }
            finally
            {

            }
        }

        public void Dispose(bool hold = false)
        {
            if (!hold)
            {
                NumSlave = NumSlave - 1;
            }
            if (NumSlave > 0) { return; }

            Status = DriverStatus.Disconnected;
            SerialClose();
        }
        public string AutoFindSerialPort(string Caption = "Pongee", string Manufact = "Prolific") // mặc định theo hãng nhà máy hay xài
        {
            using (ManagementClass i_Entity = new ManagementClass("Win32_PnPEntity"))
            {
                const String CUR_CTRL = "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\";

                foreach (ManagementObject i_Inst in i_Entity.GetInstances())
                {
                    Object o_Guid = i_Inst.GetPropertyValue("ClassGuid");
                    if (o_Guid == null || o_Guid?.ToString()?.ToUpper() != "{4D36E978-E325-11CE-BFC1-08002BE10318}")
                        continue; // Skip all devices except device class "PORTS"

                    String s_Caption = i_Inst.GetPropertyValue("Caption").ToString();
                    String s_Manufact = i_Inst.GetPropertyValue("Manufacturer").ToString();
                    String s_DeviceID = i_Inst.GetPropertyValue("PnpDeviceID").ToString();
                    String s_RegEnum = CUR_CTRL + "Enum\\" + s_DeviceID + "\\Device Parameters";
                    String s_RegServ = CUR_CTRL + "Services\\BTHPORT\\Parameters\\Devices\\";
                    String s_PortName = Registry.GetValue(s_RegEnum, "PortName", "").ToString();
                    String s_BT_Dir = null; // Bluetooth port direction
                    String s_BT_Dev = "";   // Bluetooth paired device name
                    String s_BT_MAC = "";   // Bluetooth paired device MAC address

                    int s32_Pos = s_Caption.IndexOf(" (COM");
                    if (s32_Pos > 0) // remove COM port from description
                        s_Caption = s_Caption.Substring(0, s32_Pos);

                    Debug.WriteLine("Port Name:      " + s_PortName);//"COM3"
                    Debug.WriteLine("Description:    " + s_Caption); //"Pongee"
                    Debug.WriteLine("Manufacturer:   " + s_Manufact); // "Prolific"
                    Debug.WriteLine("Device ID:      " + s_DeviceID);

                    if (s_Caption == Caption && s_Manufact == Manufact)
                    {
                        return s_PortName;
                    }




                    //if (s_DeviceID.StartsWith("BTHENUM\\")) // Bluetooth
                    //{
                    //    s_BT_Dir = "Incoming";

                    //    // "{00001101-0000-1000-8000-00805f9b34fb}#7445CEA614BC_C00000000"
                    //    String s_UniqueID = Registry.GetValue(s_RegEnum, "Bluetooth_UniqueID", "").ToString();

                    //    String[] s_Split = s_UniqueID.Split('#');
                    //    if (s_Split.Length == 2)
                    //    {
                    //        s_Split = s_Split[1].Split('_');

                    //        // Ignore MAC = "000000000000"
                    //        if (s_Split.Length == 2 && s_Split[0].Trim('0').Length > 0)
                    //        {
                    //            s_BT_MAC = s_Split[0]; // 12 digits: "7445CEA614BC"
                    //            s_BT_Dir = "Outgoing";

                    //            // Read the Bluetooth device that is paired with the COM port.
                    //            Object o_BtDevice = Registry.GetValue(s_RegServ + s_BT_MAC, "Name", null);
                    //            if (o_BtDevice is Byte[])
                    //                s_BT_Dev = Encoding.UTF8.GetString((Byte[])o_BtDevice);
                    //        }
                    //    }

                    //    Debug.WriteLine("Port Direction: " + s_BT_Dir);
                    //    Debug.WriteLine("Paired Device:  " + s_BT_Dev);
                    //    Debug.WriteLine("Device MAC Adr: " + s_BT_MAC);
                    //}

                    //Debug.WriteLine("-----------------------------------");
                }
            }
            return "";
        }

    }
}
