using ArmLibrary;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SerialLibrary
{
    public class MarinoArmSerialProcessor
    {
        #region Defines

        private String inString;
        public Boolean IsDetected;
        /// <summary>
        /// This is flag indicates that the end of the arduino sensor data stream has been detected
        /// </summary>
        public Boolean IsSensorStreamEndDetected;
        private Boolean showDiag = false;
        private int arraySize;
        private SerialPort serialPort;
        public delegate void UpdatedDataEvent(List<Point> shData, List<Point> elData);
        public event UpdatedDataEvent updatedDataEvent;
        public delegate void WordDetectedEvent();
        public event WordDetectedEvent wordDetectedEvent;
        int timeStep_ms;
        int iChunk;
        bool switchBitIsOn;

        #endregion

        #region Properties
        public List<int> ShoulderSensorData { get; set; }
        public List<int> ElbowSensorData { get; set; }
        public String CheckString { get; set; }
        public Boolean IsHelloReceived { get; set; }
        #endregion

        #region Constructors

        public MarinoArmSerialProcessor(SerialPort sp, int arraySize, int t_ms)
        {
            this.arraySize = arraySize;
            serialPort = sp;
            ShoulderSensorData = new List<int>();
            ElbowSensorData = new List<int>();
            timeStep_ms = t_ms;
        }

        #endregion

        #region Consume Actions
        /// <summary>
        /// Used to consume incoming byte data from arduino and updates sensor data
        /// </summary>
        /// <param name="inByte"></param> 
        public void AddByteToList(byte inByte)
        {
            char inChar = (char)inByte;
            bool isStillCollectingData = ElbowSensorData.Count < arraySize;

            // Collect Incoming Byte Data from Arduino until Elbow Count is full
            if (isStillCollectingData)
            {
                // Range of acceptable sensor angle values is -56 to 199
                int angle;
                if (inByte < 200)
                {
                    angle = inByte;
                }
                else
                {
                    angle = inByte - 256;
                }
                if (!switchBitIsOn)
                {
                    ShoulderSensorData.Add(angle);
                    switchBitIsOn = true;
                }
                else
                {
                    ElbowSensorData.Add(angle);
                    switchBitIsOn = false;
                    chunkUpdater(false);
                }
            }
            // This detects the end of the sensor data stream from Arduino, which is terminated with the word "END"
            else
            {
                if (inChar == '\n' & this.inString.EndsWith("END"))
                {
                    this.inString = "";
                    chunkUpdater(true);
                    Console.WriteLine($"Shoulder sensor count: {ShoulderSensorData.Count}");
                    switchBitIsOn = false;

                    IsSensorStreamEndDetected = true; //this is flag that gets used in RAP receiving state
                    wordDetectedEvent(); //this causes UpdateStateMachine() to execute in the RAP
                }
                else
                {
                    // Builds string of length 3 in order to help detect the end of the stream, see above
                    if (this.inString.Length >= 3)
                    {
                        this.inString = $"{this.inString.Substring(1, 2)}{inChar}";
                    }
                    else
                    {
                        this.inString += inChar;
                    }
                }
            }
        }
        public void ListenAndCheck(byte inByte)
        {
            char inChar = (char)inByte;

            if (inChar == '\n')
            {
                if (showDiag)
                {
                    Console.WriteLine($"Ardy says: {this.inString}");
                }
                if (this.inString.Equals(CheckString))
                {
                    IsDetected = true;
                    if (this.inString.Equals("HELLO"))
                    {
                        IsHelloReceived = true;
                    }
                    wordDetectedEvent(); 

                    //Console.WriteLine(_checkString + " string was detected");
                }
                this.inString = "";
            }
            else
            {
                this.inString = this.inString + inChar;
                //Console.WriteLine(inChar);
            }
        }
        public void Listen(byte inByte)
        {
            char inChar = (char)inByte;
            if (inChar == '\n')
            {
                if (true)
                {
                    Console.WriteLine($"Ardy said: {this.inString}");
                }
                this.inString = "";
                wordDetectedEvent();
            }
            else
            {
                this.inString = this.inString + inChar;
                //Console.WriteLine(inChar);
            }
        }
        /// <summary>
        /// Listens for a "HELLO" from Arduino and repeats it back to Arduino
        /// </summary>
        /// <param name="inByte"></param>
        public void ListenCheckAndEcho(byte inByte)
        {
            char inChar = (char)inByte;

            if (inChar == '\n')
            {
                Console.WriteLine($"From Arduino: {this.inString}");

                if (this.inString.Equals(CheckString))
                {
                    IsDetected = true;
                    Console.WriteLine($"CheckString: {CheckString} detected from Arduino, Echoing {CheckString} Back to it");
                    serialPort.Write(CheckString + '\n');
                    wordDetectedEvent();
                }
                this.inString = "";
            }
            else
            {
                this.inString = this.inString + inChar;
            }
        }

        #endregion

        #region Send Methods

        public void SendNewThrowDataToArdy(ThrowData commandData)
        {
            SendMessageToArdy("NEWTHROW"); //This tells the arduino that new throw data is coming

            //var b = BuildFakeThrowCommandData();
            //var commandData = RetrieveCommandDataFromPython();
            var shoulderFloatCmdData = commandData.Data.Shoulder.Cmd;
            var shoulderCmdData = ArrayConverter.ConvertFloatArrayToIntArray(shoulderFloatCmdData);

            var b = ArrayConverter.ConvertIntArrayToByteArray(shoulderCmdData);
            bool showChunkDiag = false;
            if (arraySize != commandData.Data.Shoulder.Cmd.Length)
            {

                Console.WriteLine("!!!!!!!!!!!!!!!!!SOMETHING is wrong with array size coming from python");
                Console.WriteLine(commandData.Data.Shoulder.Cmd.Length);
            }
            int chunkSize = 50;
            int N = (int)Math.Ceiling((double)arraySize / chunkSize);
            int index;
            Console.WriteLine($"Chunking the Throw Command Data into {N} Chunks of {chunkSize} bytes each");
            for (int i = 0; i < N; i++)
            {
                index = chunkSize * i;
                if (i == N - 1)
                {
                    int lastChunk = arraySize - (chunkSize * i);
                    chunkSize = lastChunk;
                }
                if (showChunkDiag) //diagnostic for sending chunks, set to true
                {
                    Console.WriteLine($"i is equal to: {i}, chunk size is: {chunkSize} and index is {index}");
                }

                serialPort.Write(b, index, chunkSize);
                Thread.Sleep(10); // this number is critical so that the Arduino has enough time to keep eating before its buffer is filled

            }
            Thread.Sleep(10); //
            SendMessageToArdy("END"); //This tells arduino that the stream of throw command data is at its end

        }
        public void SendMessageToArdy(string message)
        {
            serialPort.Write(message + '\n');
            Console.WriteLine($"Sending Message to Arduino: {message}");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Sends chunks of point data to UI based on chunk size or lastUpdate argument
        /// </summary>
        /// <param name="lastUpdate">
        /// normally false, but if true, indicates that the last data has been received, which forces data to be sent to UI
        private void chunkUpdater(bool lastUpdate)
        {
            int start, count;

            //packet size data points that get sent to UI at a time, should be as small as possible without causing lag
            const int updateChunkSize = 10; 
            if (ElbowSensorData.Count == 1)
            {
                iChunk = 1;
            }

            bool chunkUpdate = ElbowSensorData.Count >= updateChunkSize * iChunk;

            if ( chunkUpdate || lastUpdate)
            {
                start = updateChunkSize * (iChunk - 1);
                count = ShoulderSensorData.Count - start;
                if (count > 0)
                {
                    if (updatedDataEvent != null)
                    {
                        updatedDataEvent(GetDataPointRange(ShoulderSensorData, start, count),
                            GetDataPointRange(ElbowSensorData, start, count));
                        iChunk++;
                    }
                }
            }
        }
        public List<Point> GetDataPointRange(List<int> data, int start, int count)
        {
            List<int> dataList = data.GetRange(start, count);
            List<Point> output = new List<Point>();
            for (int i = 0; i < dataList.Count; i++)
            {
                int t = timeStep_ms * (i + start);
                Point p = new Point(t, dataList[i]);
                output.Add(p);
            }

            return output;
        }

        #endregion
    }
}