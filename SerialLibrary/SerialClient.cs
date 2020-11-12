using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using Diagnostics = System.Diagnostics;

namespace SerialLibrary
{
    /* This AX-Fast Serial Library
    Developer: Ahmed Mubarak - RoofMan

    This Library Provide The Fastest & Efficient Serial Communication
    Over The Standard C# Serial Component
    */




    public class SerialClient : IDisposable
    {
        #region Defines
        private string _port;
        private int _baudRate;
        private SerialPort _serialPort;
        private Thread serThread;
        private double _PacketsRate;
        private DateTime _lastReceive;
        /*The Critical Frequency of Communication to Avoid Any Lag*/
        private const int freqCriticalLimit = 20;
        RobotArmProtocol _rap;


        #endregion

        #region Constructors
        public SerialClient(string port)
        {
            _port = port;
            _baudRate = 115200;
            _lastReceive = DateTime.MinValue;
            serThread = new Thread(new ThreadStart(SerialReceiving));
            serThread.Priority = ThreadPriority.Normal;
            serThread.Name = "SerialHandle" + serThread.ManagedThreadId;
            _rap = new RobotArmProtocol();

        }
        public SerialClient(string Port, int baudRate)
            : this(Port)
        {
            _baudRate = baudRate;
        }
        #endregion

        #region Custom Events

        public event EventHandler<DataStreamEventArgs> OnReceiving;

        #endregion

        #region Properties


        public string Port
        {
            get 
            { 
                return _port; 
            }
            private set
            {
                _port = value;
            }
        }
        public int BaudRate
        {
            get { return _baudRate; }
        }
        public string ConnectionString
        {
            get
            {
                return String.Format("[Serial] Port: {0} | Baudrate: {1}",
                    _serialPort.PortName, _serialPort.BaudRate.ToString());
            }
        }

        public RobotArmProtocol RobotArmProtocol
        {
            get
            {
                return _rap;
            }
            set
            {
                _rap = value;
            }
        }

        #endregion

        #region Methods
        #region Port Control

        public bool OpenConn()
        {
            try
            {
                if (_serialPort == null)
                    _serialPort = new SerialPort(_port, _baudRate, Parity.None);

                if (!_serialPort.IsOpen)
                {
                    _serialPort.ReadTimeout = -1;
                    _serialPort.WriteTimeout = -1;

                    _serialPort.Open();
                    if (_rap != null)
                    {
                        _rap.AssignSerialPort(_serialPort);
                    }


                    if (_serialPort.IsOpen)
                    {
                        serThread.Start(); /*Start The Communication Thread*/

                    }
                        
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
        public bool OpenConn(string port, int baudRate)
        {
            _port = port;
            _baudRate = baudRate;

            return OpenConn();
        }
        public void CloseConn()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                serThread.Abort();

                if (serThread.ThreadState == ThreadState.Aborted)
                    _serialPort.Close();
            }

            _rap.Dispose();
        }
        public bool ResetConn()
        {
            CloseConn();
            return OpenConn();
        }
        #endregion
        #region Transmit/Receive
        public void Transmit(byte[] packet)
        {
            _serialPort.Write(packet, 0, packet.Length);
        }
        public int Receive(byte[] bytes, int offset, int count)
        {
            int readBytes = 0;

            if (count > 0)
            {
                readBytes = _serialPort.Read(bytes, offset, count);
            }

            return readBytes;
        }
        #endregion
        #region IDisposable Methods
        public void Dispose()
        {
            CloseConn();

            if (_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
                Console.WriteLine($"Port {Port} is Closed and Disposed");
                Port = null;

            }
        }
        #endregion
        #endregion

        #region Threading Loops
        private void SerialReceiving()
        {
            while (serThread.ThreadState == ThreadState.Running)
            {
                
                int count = _serialPort.BytesToRead;

                /*Get Sleep Interval*/
                TimeSpan tmpInterval = (DateTime.Now - _lastReceive);

                /*Form The Packet in The Buffer*/
                byte[] buf = new byte[count];
                int readBytes = Receive(buf, 0, count);

                if (readBytes > 0)
                {
                    OnSerialReceiving(buf);
                    foreach(byte b in buf)
                    {
                        _rap.Produce(b);  
                    }
                }

                #region Frequency Control
                _PacketsRate = ((_PacketsRate + readBytes) / 2);

                _lastReceive = DateTime.Now;

                //Thread.Sleep(1);

                if ((double)(readBytes + _serialPort.BytesToRead) / 2 <= _PacketsRate && false)
                {
                    if (tmpInterval.Milliseconds > 0)
                        Thread.Sleep(tmpInterval.Milliseconds > freqCriticalLimit ? freqCriticalLimit : tmpInterval.Milliseconds);

                    /*Testing Threading Model*/
                    Diagnostics.Debug.Write(tmpInterval.Milliseconds.ToString());
                    Diagnostics.Debug.Write(" - ");
                    Diagnostics.Debug.Write(readBytes.ToString());
                    Diagnostics.Debug.Write("\r\n");
                }
                #endregion
            }

        }
        #endregion

        #region Custom Events Invoke Functions
        private void OnSerialReceiving(byte[] res)
        {
            if (OnReceiving != null)
            {
                OnReceiving(this, new DataStreamEventArgs(res));
            }
        }
        #endregion
    }

}
