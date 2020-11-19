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

// TODO - cleanup the open connection to serial port stuff, both here and in serialClient



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
        private String _startButtonText;
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
        private States state;
        public States State 
        {
            get
            {
                return state;
            }

            set
            {
                state = value;
                StartButtonText = State.ToString().ToUpper();
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
        public RobotArmProtocol RobotArmProtocol
        {
            get; 
            set; 
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
                    //StartButtonText = "CONNECT";
                    State = States.Connect;
                }
            }
        }
        public String StartButtonText
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

        #region Enums
        public enum States
        {
            Scan,
            Connect,
            Disconnect
        }

        #endregion

        #region Constructors

        public SerialSelectionViewModel()
        {
            _isConnected = false;
            _selectedPort = null;
            State = States.Scan;
            ComboBoxText = "Scan for Ports";
        }

        #endregion

        protected override void OnDeactivate(bool close)
        {
            
        }
        public void StartButton()
        {
            switch (State)
            {
                case States.Scan:
                    {
                        //Scan for available com ports
                        Ports = new BindableCollection<String>(GetAvailableComPorts());
                        Console.WriteLine($"Ports Collection Count: {Ports.Count}");

                        // If there are ports
                        if (Ports.Count > 0)
                        {
                            // look for familiar ports that are arduino
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
                        break;
                    }
                case States.Connect:
                    {
                        tryToConnect();
                        break;
                    }
                case States.Disconnect:
                    {
                        //Closing Port "DISCONNECTING"
                        ProcessStateString = $"{SelectedPort} port has been disconnected";
                        CloseAndDispose();
                        State = States.Scan;
                        ComboBoxText = "Scan for Ports";
                    }
                    break;
                default:
                    break;
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

        // TODO - move this to SerialClient
        void tryToConnect()
        {
            if (RobotArmProtocol == null)
            {
                RobotArmProtocol = new RobotArmProtocol(SelectedPort);
            }
            bool isConnSuccess = OpenSelectedPortOnSerialClient();

            if (isConnSuccess)
            {
                IsConnected = true;
                Console.WriteLine($"Serial Connected to: {_selectedPort}");
                //StartButtonVisibility = Visibility.Hidden;
                ProcessStateString = $"{SelectedPort} port is connected";
                //StartButtonText = "DISCONNECT";
                State = States.Disconnect;
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
        private bool OpenSelectedPortOnSerialClient()
        {
            RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
            return RobotArmProtocol.SerialClient.OpenConn(SelectedPort, 115200);
        }

        // TODO - move this to SerialClient
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
        }


        public void CloseAndDispose()
        {
            try
            {
                if (RobotArmProtocol.SerialClient != null)
                {
                    RobotArmProtocol.Dispose();
                }
            }
            catch
            { }

            SelectedPort = null;
            IsConnected = false;
            Ports = null;
        }
    }
}
