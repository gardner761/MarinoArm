using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SerialLibrary;
using System.IO.Ports;
using static SerialLibrary.RobotArmProtocol;

namespace WPFUI.ViewModels
{
    public class SerialSelectionViewModel : Screen
    {
        #region Defines

        int storedResponses;
        int maxLen;
        int minLen;
        int currentLen;
        int sumLen;
        DateTime _startTime;
        TimeSpan tmpInterval;
        private BindableCollection<String> _ports;
        private string _comboBoxText;
        private string _startButtonText;
        private string _selectedPort;
        private String _processStateString;
        private String _rapStateString;
        private Visibility _startButtonVisibility;
        private bool _isConnected;
        public string[] knownArduinoPorts = new string[] { "COM4", "COM3" };
        public delegate void ConnectionMadeEvent();
        public event ConnectionMadeEvent connectionMadeEvent;

        #endregion

        #region Properties
        private bool _isAutoConnect = true;

        public bool IsAutoConnect
        {
            get { return _isAutoConnect; }
            set 
            { 
                _isAutoConnect = value;
                NotifyOfPropertyChange(() => IsAutoConnect);
            }
        }


        public BindableCollection<String> Ports
        {
            get
            {
                return _ports;
            }
            set
            {
                _ports = value;
                NotifyOfPropertyChange(() => Ports);
            }

        }

        private SerialClient serialClient;

        public SerialClient SerialClient
        {
            get { return serialClient; }
            set { serialClient = value; }
        }


        public string ComboBoxText
        {
            get
            {
                return _comboBoxText;
            }
            set
            {
                _comboBoxText = value;
                NotifyOfPropertyChange(() => ComboBoxText);
            }
        }

        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            set { _isConnected = value; }
        }
        public String ProcessStateString
        {
            get
            {
                return _processStateString;
            }

            set
            {
                _processStateString = value;
                NotifyOfPropertyChange(() => ProcessStateString);
            }
        }

        public String RAPStateString
        {
            get
            {
                return _rapStateString;
            }

            set
            {
                _rapStateString = value;
                NotifyOfPropertyChange(() => RAPStateString);
            }
        }
        public string SelectedPort
        {
            get
            {
                return _selectedPort;
            }
            set
            {
                _selectedPort = value;
                NotifyOfPropertyChange(() => SelectedPort);
                if (_selectedPort != null)
                {
                    Console.WriteLine($"Port Selected: {_selectedPort}");
                    StartButtonText = "CONNECT";
                }
            }
        }
        public string StartButtonText
        {
            get
            {
                return _startButtonText;
            }
            set
            {
                _startButtonText = value;
                NotifyOfPropertyChange(() => StartButtonText);
            }
        }
        public Visibility StartButtonVisibility
        {
            get
            {
                return _startButtonVisibility;
            }
            set
            {
                _startButtonVisibility = value;
                NotifyOfPropertyChange(() => StartButtonVisibility);
            }
        }

        #endregion

        #region Constructors
        public SerialSelectionViewModel()
        {
            _isConnected = false;
            _selectedPort = null;
            StartButtonText = "SCAN";
            ComboBoxText = "Scan for Ports";
           
        }
        #endregion

        protected override void OnDeactivate(bool close)
        {

        }

        public bool CanStartButton()
        {
            return true;
        }

        public void StartButton()
        {
            if (!IsConnected)
            {
                if (SelectedPort == null || Ports == null) //"SCAN"
                {
                    Ports = new BindableCollection<String>(GetAvailableComPorts());
                    Console.WriteLine($"Ports Collection Count: {Ports.Count}");
                    
                    if (Ports.Count > 0)
                    {
                        string checkPort = checkForKnownArduinoPorts(knownArduinoPorts);
                        if (IsAutoConnect & checkPort != null)
                        {
                            SelectedPort = checkPort;
                            tryToConnect();
                        }
                        else
                        {
                            ProcessStateString = "Choose a Port";
                            ComboBoxText = "Ports";
                        }
                    }
                    else
                    {
                        ProcessStateString = $"No Ports Available, Rescan";
                    }
                }
                else
                {
                    // Opening port "CONNECT"
                    tryToConnect();
                }
            }
            else
            {
                //Closing Port "DISCONNECTING"
                ProcessStateString = $"{SelectedPort} port has been disconnected";
                CloseAndDispose();
                StartButtonText = "SCAN";
                ComboBoxText = "Scan for Ports";
            }
        }

        private string checkForKnownArduinoPorts(string[] arduinoPorts)
        {
            string portNameWithHighestPriority = null;
            int priorityNumber = arduinoPorts.Length;
            foreach (string portName in Ports)
            {
                int i = 0;
                foreach (string apname in arduinoPorts)
                {
                    if (portName == apname & i < priorityNumber)
                    {
                        portNameWithHighestPriority = apname;
                        priorityNumber = i;
                        break;
                    }
                    i++;
                }
            }
            return portNameWithHighestPriority;
        }


        void tryToConnect()
        {
            bool isConnSuccess = openConn();

            if (isConnSuccess)
            {
                IsConnected = true;
                Console.WriteLine($"Serial Connected to: {_selectedPort}");
                //StartButtonVisibility = Visibility.Hidden;
                ProcessStateString = $"{SelectedPort} port is connected";
                StartButtonText = "DISCONNECT";
                if (connectionMadeEvent != null)
                {
                    connectionMadeEvent();
                }

            }
            else
            {
                ProcessStateString = $"{SelectedPort} port cannot connect";
            }
        }

        private bool openConn()
        {
         
            serialClient = new SerialClient(SelectedPort, 115200);
            serialClient.RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
            serialClient.OnReceiving += new EventHandler<DataStreamEventArgs>(receiveHandler);
            return serialClient.OpenConn(SelectedPort, 115200);
        }

        public List<String> GetAvailableComPorts()
        {
            List<String> output = new List<String>();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                output.Add(port);
                //Console.WriteLine(port);
            }
            return output;
        }

        private void RobotArmProtocol_stateChangedEvent(string message)
        {
            RAPStateString = $"RAP State: {message}";
            switch (SerialClient.RobotArmProtocol.state)
            {
                case States.OnStartup:
                    break;
                case States.Initialized:
                    break;
                case States.Idle:
                    break;
                case States.TaskPlanning:
                    break;
                case States.Sending:
                    break;
                case States.Starting:
                    break;
                case States.Receiving:
                    break;
                case States.Done:
                    break;
                case States.Error:
                    break;
                default:
                    break;
            }
        }


        private void receiveHandler(object sender, DataStreamEventArgs e)
        {
            if (storedResponses == 1)
            {
                _startTime = DateTime.Now;
            }

            storedResponses++;
            minLen = minLen > e.Response.Length ? e.Response.Length : minLen;
            maxLen = maxLen < e.Response.Length ? e.Response.Length : maxLen;
            currentLen = e.Response.Length;
            sumLen += currentLen;
            if (sumLen == 1000)
            {
                tmpInterval = (DateTime.Now - _startTime);
                Console.WriteLine(e.Response[0]);
            }
        }
        public void CloseAndDispose()
        {
            try
            {
                if (serialClient != null)
                {
                    serialClient.CloseConn();
                    serialClient.OnReceiving -= new EventHandler<DataStreamEventArgs>(receiveHandler);
                    serialClient.Dispose();
                }
            }
            catch
            { }

            //clearLists();
           
            SelectedPort = null;
            IsConnected = false;
            Ports = null;
        }

        public List<Point> GetDataPoints()
        {
            List<int> ssd = serialClient.RobotArmProtocol.ShoulderSensorData;
            List<Point> output = new List<Point>();
            for (int i = 0; i < ssd.Count; i++)
            {
                Point p = new Point(i+1, ssd[i]);
                output.Add(p);
            }
            Console.WriteLine($"Received Data Points From Ardy Count: {ssd.Count}");
            return output;
        }

    }
}
