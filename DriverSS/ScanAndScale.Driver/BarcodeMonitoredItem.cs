using CoreScanner;
using ScanAndScale.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ScanAndScale.Driver
{
    public class BarcodeMonitoredItem
    {
        #region Fields and Properties

        private static CCoreScanner cCoreScannerClass;

        private DataValue _datavalue = new DataValue(DriverStatus.Unknown, null);
        public DataValue DataValue
        {
            get => _datavalue;
            set
            {
                var oldValue = _datavalue;
                _datavalue = value;
                _dataValueChanged?.Invoke(this, new DataValueChangedEventArgs(oldValue, value));
            }
        }

        private event EventHandler<DataValueChangedEventArgs> _dataValueChanged;
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }


        private bool _EnConnectect = false;
        public bool EnConnectect
        {
            get { return _EnConnectect; }
            set { _EnConnectect = value; }
        }
        #endregion

        public void Dispose()
        {
            cCoreScannerClass.BarcodeEvent -= new _ICoreScannerEvents_BarcodeEventEventHandler(OnBarcodeEvent);
            int status; // Extended API return code
            cCoreScannerClass.Close(0, out status);
        }


        #region Singleton Pattern

        private static BarcodeMonitoredItem _instance;

        public static BarcodeMonitoredItem Instance
        {
            private set => _instance = value;
            get
            {
                if (_instance == null)
                {
                    _instance = new BarcodeMonitoredItem();
                }
                return _instance;
            }
        }

        public BarcodeMonitoredItem()
        {
            var asm = this.GetType().Assembly;
            InitializeScanner();
        }

        #endregion

        #region Initialization
        private void InitializeScanner()
        {
            //Instantiate CoreScanner Class
            cCoreScannerClass = new CCoreScanner();
            //Call Open API
            short[] scannerTypes = new short[1]; // Scanner Types you are interested in
            scannerTypes[0] = 2; // 1 for all scanner types
            short numberOfScannerTypes = 1; // Size of the scannerTypes array
            int status; // Extended API return code
            cCoreScannerClass.Open(0, scannerTypes, numberOfScannerTypes, out status);
            // Lets list down all the scanners connected to the host
            short numberOfScanners; // Number of scanners expect to be used
            int[] connectedScannerIDList = new int[255];
            // List of scanner IDs to be returned
            string outXML; //Scanner details output
            cCoreScannerClass.GetScanners(out numberOfScanners, connectedScannerIDList,
            out outXML, out status);
            Console.WriteLine(outXML);

            // Subscribe for barcode events in cCoreScannerClass
            cCoreScannerClass.BarcodeEvent += new _ICoreScannerEvents_BarcodeEventEventHandler(OnBarcodeEvent);

            // Let's subscribe for events
            int opcode = 1001; // Method for Subscribe events

            string inXML = "<inArgs>" +
            "<cmdArgs>" +
            "<arg-int>1</arg-int>" + // Number of events you want to subscribe
            "<arg-int>1</arg-int>" + // Comma separated event IDs
            "</cmdArgs>" +
            "</inArgs>";
            cCoreScannerClass.ExecCommand(opcode, ref inXML, out outXML, out status);
            Console.WriteLine(outXML);

            inXML = "<inArgs>" +
           "<cmdArgs>" +
           "<arg-int>2</arg-int>" + // Number of events you want to subscribe
           "<arg-int>1</arg-int>" + // Comma separated event IDs
           "</cmdArgs>" +
           "</inArgs>";
            cCoreScannerClass.ExecCommand(opcode, ref inXML, out outXML, out status);
            Console.WriteLine(outXML);

            inXML = "<inArgs>" +
           "<cmdArgs>" +
           "<arg-int>3</arg-int>" + // Number of events you want to subscribe
           "<arg-int>1</arg-int>" + // Comma separated event IDs
           "</cmdArgs>" +
           "</inArgs>";
            cCoreScannerClass.ExecCommand(opcode, ref inXML, out outXML, out status);
            Console.WriteLine(outXML);
        }

        #endregion

        #region Event Handling
        private void OnBarcodeEvent(short eventType, ref string pscanData)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(pscanData);
            var scannerId = xmlDoc.GetElementsByTagName("scannerID");

           // if (scannerId[0].InnerText == "1" || scannerId[0].InnerText == "3")
            {
                var barcode = AsciiToString(xmlDoc.GetElementsByTagName("datalabel")[0].InnerText);
                DataValue = new DataValue(DriverStatus.Connected, barcode);
            }
        }


        #endregion

        #region Utility Methods
        private string AsciiToString(string contentStr)
        {
            string returnValue = null;

            foreach (var item in contentStr.Split(' '))
            {
                int n = Convert.ToInt32(item, 16);
                returnValue += (char)n;
            }

            return returnValue;
        }

        #endregion
    }
}
