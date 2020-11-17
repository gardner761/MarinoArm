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
        public Boolean IsDetected, IsFinished;
        private Boolean showDiag = false;
        private int arraySize;
        private SerialPort serialPort;
        public delegate void UpdatedDataEvent(List<Point> data);
        public event UpdatedDataEvent updatedDataEvent;
        public delegate void WordDetectedEvent();
        public event WordDetectedEvent wordDetectedEvent;
        int timeStep_ms;
        int iChunk;

        #endregion

        #region Properties
        public List<int> ShoulderSensorData { get; set; }
        public String CheckString { get; set; }
        public Boolean IsHelloReceived { get; set; }
        #endregion

        #region Constructors

        public MarinoArmSerialProcessor(SerialPort sp, int arraySize, int t_ms)
        {
            this.arraySize = arraySize;
            serialPort = sp;
            ShoulderSensorData = new List<int>();
            timeStep_ms = t_ms;
        }

        #endregion

        #region Consume Actions
        /// <summary>
        /// Used to consume incoming byte data from arduino and updates shoulder sensor data
        /// </summary>
        /// <param name="inByte"></param> 
        public void AddByteToList(byte inByte)
        {
            char inChar = (char)inByte;

            if (inChar == '\n' & this.inString.EndsWith("END"))
            {
                IsFinished = true;
                this.inString = "";
                ShoulderSensorData.RemoveRange(ShoulderSensorData.Count - 3, 3);
                chunkUpdater(true);
                Console.WriteLine($"Shoulder sensor count: {ShoulderSensorData.Count}");
                wordDetectedEvent();
            }
            else
            {

                if (this.inString.Length >= 3)
                {
                    this.inString = $"{this.inString.Substring(1, 2)}{inChar}";
                }
                else
                {
                    this.inString += inChar;
                }
                int angle;
                if (inByte < 200)
                {
                    angle = inByte;
                }
                else
                {
                    angle = inByte - 256;
                }
                ShoulderSensorData.Add(angle);
                if (ShoulderSensorData.Count <= arraySize)
                {
                    chunkUpdater(false);
                }
            }
        }
        public void ListenAndCheck(byte inByte)
        {
            char inChar = (char)inByte;
            bool isEqual = false;
            if (inChar == '\n')
            {
                if (showDiag)
                {
                    Console.WriteLine($"Ardy says: {this.inString}");
                }
                if (this.inString.Equals(CheckString))
                {
                    isEqual = true;
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
        // TODO - make SendNewThrowDataToArdy async or on it's own thread from the calling source (RAP)
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

        private void chunkUpdater(bool updateAll)
        {
            int start, count;
            const int updateChunkSize = 10;
            if (ShoulderSensorData.Count == 1)
            {
                iChunk = 1;
            }

            if (ShoulderSensorData.Count >= updateChunkSize * iChunk || updateAll)
            {
                start = updateChunkSize * (iChunk - 1);
                count = ShoulderSensorData.Count - start;
                if (count > 0)
                {
                    if (updatedDataEvent != null)
                    {
                        updatedDataEvent(GetDataPointRange(start, count));
                        iChunk++;
                    }
                }
            }
        }
        public List<Point> GetDataPointRange(int start, int count)
        {
            List<int> ssd = ShoulderSensorData.GetRange(start, count);
            List<Point> output = new List<Point>();
            for (int i = 0; i < ssd.Count; i++)
            {
                int t = timeStep_ms * (i + 1 + start);
                Point p = new Point(t, ssd[i]);
                output.Add(p);
            }
            //Console.WriteLine($"Updating {ssd.Count} New Data Points From Ardy to Plot, range is {start}, {count}");
            return output;
        }
    }
}
