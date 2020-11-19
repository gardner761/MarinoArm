using System;
using System.IO.Ports;
using System.Threading;
using Diagnostics = System.Diagnostics;


/// <summary>
///    This Library Provides Fast & Efficient Serial Communication
///    Over The Standard C# Serial Component
/// </summary>
namespace SerialLibrary
{
    public class SerialClient : IDisposable
    {
        #region Defines

        private string portName;
        private int _baudRate;
        public SerialPort SerialPort;
        private Thread readSerialPortThread;
        private DateTime _lastReceive;
        public delegate void ConnectionMadeEvent();
        public event ConnectionMadeEvent connectionMadeEvent;
        public delegate void AddByteToQueueEvent(byte data);
        public event AddByteToQueueEvent addByteToQueueEvent;

        /// <summary>
        /// set this to be true when you want to stop the thread
        /// </summary>
        bool stopReadingThreadRequest;

        #endregion

        #region Constructors
        public SerialClient(string port)
        {
            portName = port;
            _baudRate = 115200;
            _lastReceive = DateTime.MinValue;

            // Setup the thread to read the incoming serial data
            stopReadingThreadRequest = false;
            readSerialPortThread = new Thread(new ThreadStart(ReadSerialPortAndAddBytesToQueue));
            readSerialPortThread.Priority = ThreadPriority.Normal;
            readSerialPortThread.Name = "IncomingSerialReadingThread - ID:" + readSerialPortThread.ManagedThreadId;

        }
        public SerialClient(string Port, int baudRate)
            : this(Port)
        {
            _baudRate = baudRate;
        }
        #endregion

        #region Properties

        public string Port
        {
            get 
            { 
                return portName; 
            }
            private set
            {
                portName = value;
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
                    SerialPort.PortName, SerialPort.BaudRate.ToString());
            }
        }

        #endregion

        #region Methods
        #region Port Control

        public bool OpenConn()
        {
            try
            {
                if (SerialPort == null)
                {
                    SerialPort = new SerialPort(portName, _baudRate, Parity.None);
                }

                if (!SerialPort.IsOpen)
                {
                    SerialPort.ReadTimeout = -1;
                    SerialPort.WriteTimeout = -1;
                    SerialPort.Open();
                    if (SerialPort.IsOpen)
                    {
                        readSerialPortThread = new Thread(new ThreadStart(ReadSerialPortAndAddBytesToQueue));
                        readSerialPortThread.Start(); /*Start The read serial Thread*/
                    }
                        
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

            connectionMadeEvent();
            return true;
        }
        public bool OpenConn(string port, int baudRate)
        {
            portName = port;
            _baudRate = baudRate;

            return OpenConn();
        }
        public void CloseConn()
        {
            if (SerialPort != null && SerialPort.IsOpen)
            {
                readSerialPortThread.Abort();
                while (readSerialPortThread.IsAlive){}
                SerialPort.Close();
                SerialPort.Dispose();
            }

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
            SerialPort.Write(packet, 0, packet.Length);
        }
        public int Receive(byte[] bytes, int offset, int count)
        {
            int readBytes = 0;

            if (count > 0)
            {
                readBytes = SerialPort.Read(bytes, offset, count);
            }

            return readBytes;
        }
        #endregion
        #region IDisposable Methods
        public void Dispose()
        {
            CloseConn();

            if (SerialPort != null)
            {
                SerialPort.Dispose();
                SerialPort = null;
                Console.WriteLine($"Port {Port} is Closed and Disposed");
                Port = null;

            }
        }
        #endregion
        #endregion

        #region Threading Loops

        /// <summary>
        /// This method reads incoming serial port bytes and stores them in a queue inside the apcq, 
        /// it should be used on its own thread. 
        /// </summary>
        private void ReadSerialPortAndAddBytesToQueue()
        {
            // TODO - Stop and start this serial reading thread with RAP, or maybe just leave it?
            while (!stopReadingThreadRequest & readSerialPortThread.ThreadState == ThreadState.Running)
            {
                while(addByteToQueueEvent==null)
                {
                    //stay here until this is no longer null
                }

                /*Measures Interval Between Cycles*/
                TimeSpan tmpInterval = (DateTime.Now - _lastReceive);

                /*Read the serial port bytes and send them to the apcq queue via rap*/
                int count = SerialPort.BytesToRead;
                if (count > 0)
                {
                    byte[] buf = new byte[count];
                    SerialPort.Read(buf, 0, count);
                    foreach (byte b in buf)
                    {
                        addByteToQueueEvent(b); 
                    }
                    //Console.WriteLine($"Buf says: {buf}");
                }

                _lastReceive = DateTime.Now;

            }
        }
        #endregion
    }
}