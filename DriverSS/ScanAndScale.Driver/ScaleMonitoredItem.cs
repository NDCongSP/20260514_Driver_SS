using ScanAndScale.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static System.Formats.Asn1.AsnWriter;

namespace ScanAndScale.Driver
{
    public class ScaleMonitoredItem : IDisposable
    {
        // Singleton
        //  private static ScaleMonitoredItem _instance;
        private static readonly object LockObject = new object();
        #region Feild and Property
        // Biến và đối tượng cần thiết
        private TcpClient clientSocket;
        private Socket _socket;
        private System.Timers.Timer readScaleTimer;
        private Regex digits = new Regex(@"^\D*?((-?(\d+(\.\d+)?)|(-?\.\d+))).*");
        private object _lockObj = new object();
        private int _countDisconnect = 0;
        private double _oldScale = 0;
        private double _scaleValue = 0;
        private string _status = "Disconnected";

        private object myLibraryInstance;
        private MethodInfo getWeightMethod;

        public int CountDisconnect { get; private set; }


        private bool EnConnectect = true;
        private string Ip { get; set; } = "192.168.80.237";
        private int Port { get; set; } = 23;
        private int TimeScan { get; set; } = 1000;
        private double CalibZero { get; set; } = 0;
        private double CalibGain { get; set; } = 1;
        private string ScaleModelName { get; set; } = "Scale_SampleReading";
        private int DecimalNum { get; set; } = 3;


        private DriverStatus _Status;

        public DriverStatus Status
        {
            get { return _Status; }
            private set
            {
                if (_Status != value)
                {
                    _Status = value;
                    DataValue = new DataValue(_Status, _Value);
                }
            }
        }

        private double _Value;

        public double Value
        {
            get { return _Value; }
            private set
            {
                _Value = value;
                DataValue = new DataValue(_Status, _Value);
            }
        }

        public bool? Stable { get; set; } = true;
        public bool? Tare { get; set; } = false;
        public string Unit { get; set; } = "KG";

        private DataValue _datavalue = new DataValue(DriverStatus.Unknown, null);
        public string? rawData { private set; get; }
        public string? rawDataNotReadline { private set; get; }
        public bool IsReadBuffer { get; set; } = false;

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
        #endregion

        // Sự kiện thay đổi giá trị
        private event EventHandler<DataValueChangedEventArgs> _dataValueChanged;
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged
        {
            add => _dataValueChanged += value;
            remove => _dataValueChanged -= value;
        }

        public ScaleMonitoredItem(bool? enable, string ip, int port, int timescan, double calibZero, double calibGain, string scaleModelName, int decimalNum)
        {
            EnConnectect = enable == true;
            Ip = ip;//"10.40.9.22";
            Port = port;
            TimeScan = timescan;
            CalibZero = calibZero;
            CalibGain = calibGain;
            ScaleModelName = scaleModelName;
            DecimalNum = decimalNum;
        }
        //Run
        public async void CheckConnect()
        {
            if (!EnConnectect)
                return;

            string dllPath = $"{ScaleModelName}.dll";
            Assembly myLibraryAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
            Type myLibraryType = myLibraryAssembly.GetType($"{ScaleModelName}.ScaleReading");
            // Lấy MethodInfo cho phương thức GetWeight
            getWeightMethod = myLibraryType.GetMethod("GetWeight",
                new[] {
                    typeof(double?).MakeByRefType(),
                    typeof(bool?).MakeByRefType(),
                    typeof(bool?).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                    typeof(string)
                });

            // Tạo một đối tượng từ lớp trong DLL
            myLibraryInstance = Activator.CreateInstance(myLibraryType);


            await ConnectAsync();
            readScaleTimer = new System.Timers.Timer(TimeScan); //TimeScan
            readScaleTimer.Elapsed += OnReadScaleTimerElapsed;
            readScaleTimer.Start();

        }
        private async void OnReadScaleTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (readScaleTimer == null) return;
                readScaleTimer?.Stop();
                if (clientSocket == null || !clientSocket.Connected)
                {
                    Status = DriverStatus.Disconnected;
                    //Status = DriverStatus.Reconnecting;
                    //await Reconnect();
                }

                if (clientSocket != null && clientSocket.Connected)
                {
                    Status = DriverStatus.Connected;
                    ReadScale();
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex}");
                Status = DriverStatus.Disconnected;
                // Status = DriverStatus.Reconnecting;
                // await Reconnect();
                //return;
            }
            finally
            {
                if (readScaleTimer != null)
                    readScaleTimer?.Start();
            };

        }
        public async Task ConnectAsync()
        {
            try
            {
                clientSocket = new TcpClient();
                await clientSocket.ConnectAsync(Ip, Port);
                _socket = clientSocket.Client;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Connect Scale: {ex.Message}");
            }
        }
        public void Connect()
        {
            try
            {
                clientSocket = new TcpClient();
                clientSocket.Connect(Ip, Port);
                _socket = clientSocket.Client;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Connect Scale: {ex.Message}");
            }
        }
        public void Disconnect()
        {
            try
            {
                // Hủy TcpClient
                if (clientSocket != null)
                {
                    clientSocket.Close();
                    clientSocket.Dispose();
                    clientSocket = null;
                }
                // Hủy Socket
                if (_socket != null)
                {
                    try
                    {
                        _socket.Close();
                        _socket = null;
                        //_socket.Shutdown(SocketShutdown.Both);
                    }
                    catch (SocketException ex)
                    {
                        // Xử lý ngoại lệ nếu cần
                    }
                    finally
                    {
                        //_socket.Close();
                        //_socket = null;
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Disconnect Scale: {ex.Message}");

            }
        }
        //public async void CheckConnectAsync()
        //{
        //    while (!StopScale)
        //    {
        //        try
        //        {
        //            Ping ping = new Ping();
        //            PingReply reply = await ping.SendPingAsync(Ip);

        //            CountDisconnect = reply.Status != IPStatus.Success ? CountDisconnect + 1 : 0;

        //            if (CountDisconnect >= 3)
        //            {
        //                Status = "Disconnected";
        //                CountDisconnect = 0;
        //                cancelTokenSrc.Cancel();
        //                Reconnect();
        //            }
        //        }
        //        catch { }

        //        await Task.Delay(1000);
        //    }
        //}
        public async Task Reconnect()
        {
            Disconnect();
            await Task.Delay(1000);
            Connect();
        }
        private async void ReadScale()
        {
            try
            {
                using (NetworkStream _stream = new NetworkStream(_socket))
                {
                    using (StreamReader _reader = new StreamReader(_stream))
                    {
                        if (IsReadBuffer)
                        {
                            char[] buffer = new char[1000];
                            int charsRead = await _reader.ReadAsync(buffer, 0, buffer.Length);
                            rawDataNotReadline = new string(buffer, 0, charsRead);
                        }

                        rawData = _reader.ReadLine();
#if DEBUG
                        //Debug.WriteLine(rawData);
#endif

                        // Khai báo các tham số cần thiết
                        object[] parameters = new object[] { null, null, null, "", rawData };
                        // Gọi phương thức GetWeight và truyền tham số
                        getWeightMethod.Invoke(myLibraryInstance, parameters);
                        // Lấy giá trị trả về từ tham số out
                        Value = ((double?)parameters[0]).Value;
                        if (Value != 0)
                        {
                            Value = Math.Round((Value + CalibZero) * CalibGain, DecimalNum);
                        }
                        Stable = (bool?)parameters[1];
                        Tare = (bool?)parameters[2];
                        Unit = (string?)parameters[3];

#if DEBUG
                        //Debug.WriteLine("ssssssss      " + Tare.ToString());

                        // In ra giá trị đã lấy được
                        //Debug.WriteLine($"Weight: {Value}, Stable: {stable}");
#endif

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ReadScale: {ex}");
            }
        }
        public void Dispose()
        {
            Disconnect();
            if (readScaleTimer != null)
            {
                // readScaleTimer.Elapsed -= OnReadScaleTimerElapsed;
                readScaleTimer.Dispose();
                readScaleTimer = null;
            }
        }
        // Phương thức thiết lập giá trị mới và thông báo nếu giá trị đã thay đổi
        private void SetAndNotifyIfChanged<T>(ref T field, T value, Action<T> onChanged = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                onChanged?.Invoke(value);
            }
        }
    }
}



