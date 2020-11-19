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

        #region UI Actions
        public void StartButton()
        {
            switch (State)
            {
                case States.Scan:
                    {
                        //Scan for available com ports
                        Ports = new BindableCollection<String>(SerialClient.GetAvailableComPorts());
                        Console.WriteLine($"Ports Collection Count: {Ports.Count}");

                        // If there are ports
                        if (Ports.Count > 0)
                        {
                            // look for familiar ports that are arduino
                            string checkPort = checkForKnownArduinoPorts(knownArduinoPorts);
                            if (IsAutoConnect & checkPort != null)
                            {
                                SelectedPort = checkPort;
                                TryToConnect();
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
                        TryToConnect();
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

        #endregion

        #region Helper Methods
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

        void TryToConnect()
        {
            if (RobotArmProtocol == null)
            {
                RobotArmProtocol = new RobotArmProtocol();
                RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
            }

            bool isConnSuccess = RobotArmProtocol.SerialClient.OpenConn(SelectedPort, SerialVariables.BAUD_RATE);

            if (isConnSuccess)
            {
                IsConnected = true;
                Console.WriteLine($"Serial Connected to: {SelectedPort}");
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

        #endregion

        #region Event Handlers
        private void RobotArmProtocol_stateChangedEvent(string message)
        {
            RAPStateString = $"RAP State: {message}";
        }

        #endregion
    }
}
