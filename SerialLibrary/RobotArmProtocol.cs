using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using ArmLibrary;
using System.ComponentModel;
using PythonIntegration;
using System.Configuration;
using Google.Protobuf.WellKnownTypes;
using System.Timers;
using static ArmLibrary.GlobalVariables;

namespace SerialLibrary
{
    //
    public class RobotArmProtocol
    {
        #region Defines
        public delegate void StateChangedEvent(string message);
        public event StateChangedEvent stateChangedEvent;
        public delegate void UpdatedDataEvent(List<Point> data);
        public event UpdatedDataEvent updatedDataEvent;
        public delegate void ReferenceDataEvent(List<Point> data);
        public event ReferenceDataEvent referenceDataEvent;
        public delegate void CommandDataEvent(List<Point> data);
        public event CommandDataEvent commandDataEvent;
        private string inString;
        private bool showDiag=true;
        bool isConnected;
        public int refreshPlotCount;
        bool isStarted, isFinished;
        bool isHelloReceived;
        string _checkString;
        List<int> _shoulderSensorData;
        Stopwatch stopWatch;
        SerialPort _port;
        AsyncProducerConsumerQueue<byte> _apcq;
        public States state; 
        int iChunk;
        int updateChunkSize = 10;
        int _throwCtr;
        static double throwDuration_sec = 2.0;
        static int arraySize=(int)((double)GlobalVariables.SAMPLING_FREQUENCY*throwDuration_sec) + 1; //1100 is close to the max for Unos, not sure about Megas
        int timeStep_ms = 1000 / GlobalVariables.SAMPLING_FREQUENCY;
        private ThrowData throwData;
        
        bool startingWithSavedPythonFile = false;
        System.Timers.Timer timer;
   

        #endregion

        #region Properties

        public int ThrowCtr
        {
            get { return _throwCtr; }
            set { _throwCtr = value; }
        }

        public bool ThrowRequested;

        public List<int> ShoulderSensorData
        {
            get { return _shoulderSensorData; }
            //set {_shoulderSensorData = value;}
        }

        #endregion

        #region Enums
        public enum States
        {
            OnStartup = 0,
            Initialized = 1,
            Idle = 10,
            TaskPlanning = 20,
            Calculating,
            Sending,
            Starting,
            Receiving,
            Done = 100,
            Error = 911
        }
        #endregion

        #region Constructors

        public RobotArmProtocol()
        {
            state = States.OnStartup;
        }
 
        #endregion

        #region State Machine
        private void ChangeStateTo(States nextstate)
        {
            state = nextstate;
            string message = state.ToString();
            Console.WriteLine($"RAP State is: {message}");
            if (stateChangedEvent != null)
            {
                stateChangedEvent(message);
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            UpdateStateMachine();
            Console.WriteLine("Timer Elapsed");
        }

        public void UpdateStateMachine()
        {
            if(timer == null)
            {
                timer = new System.Timers.Timer(1000);
                timer.Elapsed += timer_Tick;
                //timer.Start();
            }
            switch (state)
            {
                case States.OnStartup:
                    {
                        if (_apcq == null)
                        {
                            _apcq = new AsyncProducerConsumerQueue<byte>(ListenAndEchoHello);
                        }
                        ThrowCtr = 0;
                        throwData = new ThrowData();
                        throwData.WriteFirstThrowDataToJson(writeToJsonFilePath); //Initialize ThrowData
                        Console.WriteLine("Initializing First Throw Json File");
                       
                        ChangeStateTo(States.Initialized);
                        break;
                    }
                case States.Initialized:
                    {
                        if (isHelloReceived)
                        {
                            SendMessageToArdy(arraySize.ToString());
                            _apcq.SwitchConsumerActionTo(ListenAndCheck);
                            ThrowRequested = false;
                            ChangeStateTo(States.Idle);
                        }
                        break;
                    }
                case States.Idle:
                    {
                        if (ThrowRequested)
                        {
                            ChangeStateTo(States.Calculating);
                            //UpdateStateMachine();
                        }

                        break;
                    }

                case States.Calculating:
                    {
                        
                            ThrowCtr++;
                            _checkString = "START"; //this use to be after SwitchConsumerActionTO(listenAndCheck)
                            _apcq.SwitchConsumerActionTo(ListenAndCheck);
                            bool pythonExecutedSuccessfully = false;
                            
                            if (ThrowCtr == 1 & startingWithSavedPythonFile)
                            {

                            }
                            else
                            {
                                startingWithSavedPythonFile = false;
                                pythonExecutedSuccessfully = ExecutePythonNewThrowCalculation();
                            }
                            if (pythonExecutedSuccessfully || startingWithSavedPythonFile)
                            {
                                if (ThrowCtr == 1 || ThrowCtr % refreshPlotCount == 1) //if this is the first throw, then want to read the ref signal and have it plotted
                                {
                                    LoadReferenceDataFromJson();
                                }
                                throwData = LoadCommandDataFromJson();
                                if(startingWithSavedPythonFile)
                                {
                                    throwData.Data.TrialNumber = ThrowCtr;
                                }
 
                                if (throwData.Data.TrialNumber != ThrowCtr )
                                {
                                    Console.WriteLine($"!!!!!!!!!!Big error in code: mismatch between internal throw counter and python counter: {ThrowCtr} to {throwData.Data.TrialNumber}");
                                    ChangeStateTo(States.Error);
                                }

                                SendNewThrowDataToArdy(throwData);

                                isStarted = false;
                                ChangeStateTo(States.Starting);
                            }
                            else //Python did not execute successfully
                            {
                                Console.WriteLine("Python did not execute successfully, debugging needed");
                                ChangeStateTo(States.Error);
                            }
                        
                        //showDiag = true;
                        break;
                    }
                case States.Starting:
                    {
                        if (isStarted)
                        {
                            _shoulderSensorData = new List<int>();
                            _checkString = "END";
                            _shoulderSensorData.Clear();
                            _apcq.SwitchConsumerActionTo(AddByteToList);
                            isFinished = false;
                            ChangeStateTo(States.Receiving);
                        }
                        break;
                    }
                case States.Receiving:
                    {
                        if (isFinished)
                        {
                            _apcq.SwitchConsumerActionTo(Listen);
                            throwData.Data.Shoulder.Sensor = _shoulderSensorData.ToArray();
                            //state = States.Done;
                            ChangeStateTo(States.Done);
                        }
                        break;
                    }
                case States.Done:
                    {
                        if (true)
                        {
                            _apcq.SwitchConsumerActionTo(ListenAndCheck);
                            ThrowRequested = false;
                            throwData.WriteDataToJsonFile(writeToJsonFilePath);

                            ChangeStateTo(States.Idle);
                        }
                        break;
                    }
                case States.Error:
                    {
                        break;
                    }
            }
        }

        #endregion

        #region Produce Actions

        public void Produce(byte b)
        {
            _apcq.Produce(b);
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
          

            if (inChar == '\n' & inString.EndsWith("END"))
            {
                //Console.WriteLine($"End was detected");
                isFinished = true;
                clearString();
                _shoulderSensorData.RemoveRange(_shoulderSensorData.Count - 3, 3);
                chunkUpdater(true);
                //Console.WriteLine("Shoulder Sensor Data:");
                //_shoulderSensorData.ForEach(Console.WriteLine);
                UpdateStateMachine();
            }
            else
            {
                if (inString.Length >= 3)
                {
                    inString = $"{inString.Substring(1, 2)}{inChar}";
                }
                else
                {
                    inString += inChar;
                }
                int angle;
                if(inByte<200)
                {
                    angle = inByte;
                }
                else
                {
                    angle = inByte - 256;
                }
                _shoulderSensorData.Add(angle);
                if (_shoulderSensorData.Count <= arraySize)
                {
                    chunkUpdater(false);
                }

                //Console.WriteLine(inByte);
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
                    Console.WriteLine(inString);
                }
                if (inString.Equals(_checkString))
                {
                    isEqual = true;
                    isStarted = true;
                    UpdateStateMachine();
                    //Console.WriteLine(_checkString + " string was detected");
                }
                inString = "";
            }
            else
            {
                inString = inString + inChar;
                //Console.WriteLine(inChar);
            }


            if (isEqual)
            {
                //return true;
            }
            else
            {
                //return false;
            }

        }
        public void Listen(byte inByte)
        {
            char inChar = (char)inByte;
            if (inChar == '\n')
            {
                if (true)
                {
                    Console.WriteLine($"Ardy said: {inString}");
                }
                UpdateStateMachine();
                inString = "";
            }
            else
            {
                inString = inString + inChar;
                //Console.WriteLine(inChar);
            }
        }
        /// <summary>
        /// Listens for a "HELLO" from Arduino and repeats it back to Arduino
        /// </summary>
        /// <param name="inByte"></param>
        public void ListenAndEchoHello(byte inByte)
        {
            char inChar = (char)inByte;

            if (inChar == '\n')
            {
                Console.WriteLine($"From Arduino: {inString}");

                if (inString == "HELLO")
                {
                    Console.WriteLine("Hello Received from Ardy, Sending Hello Back to Arduino");
                    isHelloReceived = true;
                    _port.Write(inString + '\n');
                    UpdateStateMachine();
                }
                clearString();
            }
            else
            {
                inString = inString + inChar;
            }
        }
        private void SendNewThrowDataToArdy(ThrowData commandData)
        {
            SendMessageToArdy("NEWTHROW"); //This tells the arduino that new throw data is coming
          
            //var b = BuildFakeThrowCommandData();
            //var commandData = RetrieveCommandDataFromPython();
            var shoulderFloatCmdData = commandData.Data.Shoulder.Cmd;
            var shoulderCmdData = ConvertFloatArrayToIntArray(shoulderFloatCmdData);

            var b = ConvertIntArrayToByteArray(shoulderCmdData);
            bool showChunkDiag = false;
            if (arraySize != commandData.Data.Shoulder.Cmd.Length)
            {
                Console.WriteLine("!!!!!!!!!!!!!!!!!SOMETHING is wrong with array size coming from python");
                Console.WriteLine(commandData.Data.Shoulder.Cmd.Length);
            }
            int chunkSize = 50;
            int N = (int)Math.Ceiling((double)arraySize/chunkSize);
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

                _port.Write(b, index, chunkSize);
                Thread.Sleep(10); // this number is critical so that the Arduino has enough time to keep eating before its buffer is filled

            }
            Thread.Sleep(10); //
            SendMessageToArdy("END"); //This tells arduino that the stream of throw command data is at its end
 
        }
        public void SendMessageToArdy(string message)
        {
            _port.Write(message + '\n');
            Console.WriteLine($"Sending Message to Arduino: {message}");
        }

        #endregion

        #region Helper Methods

        public void AssignSerialPort(SerialPort port)
        {
            stopWatch = new Stopwatch();
            _port = port;
            ChangeStateTo(States.OnStartup);
            UpdateStateMachine();
        }
        private byte[] BuildFakeThrowCommandData()
        {
            byte[] b = new byte[arraySize];
            for (int i = 0; i < arraySize; i++)
            {

                if (i + 1 == 13 & false)
                {
                    b[i] = 130;
                }
                else if (i < 100)
                {
                    b[i] = (byte)(OpenLoopStaticOutput(30));
                }
                else if (i < 150)
                {
                    b[i] = (byte)(OpenLoopStaticOutput(30) + 60);
                }
                else if (i < 180)
                {
                    b[i] = (byte)(80);
                }
                else if (i < 220)
                {
                    b[i] = (byte)(OpenLoopStaticOutput(45) + 20);
                }
                else if (i < 300)
                {
                    b[i] = (byte)(OpenLoopStaticOutput(45));
                }
                //Console.WriteLine(b[i]);
            }
            return b;
        }
        private void chunkUpdater(bool updateAll)
        {
            int start, count;
            if (_shoulderSensorData.Count == 1)
            {
                iChunk = 1;
            }

            if (_shoulderSensorData.Count >= updateChunkSize * iChunk || updateAll)
            {
                start = updateChunkSize * (iChunk - 1);
                count = _shoulderSensorData.Count - start;
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
        private void clearString()
        {
            inString = "";
        }
        public int[] ConvertFloatArrayToIntArray(float[] inArray)
        {
            int[] outArray = new int[inArray.Length];
            int i = 0;
            foreach (float item in inArray)
            {
                outArray[i] = (int)item;
                i++;
            }
            return outArray;
        }
        private List<Point> ConvertIntArraysToPointList(int[] timeArray, int[] intArray)
        {
            var listData = new List<Point>();
            for (int i = 0; i < intArray.Length; i++)
            {
                Point p = new Point(timeArray[i], intArray[i]);
                listData.Add(p);
            }
            return listData;
        }
        public byte[] ConvertIntArrayToByteArray(int[] intArray)
        {
            var L = intArray.Length;
            byte[] byteArray = new byte[L];
            for (int i = 0; i < L; i++)
            {
                byteArray[i] = (byte)intArray[i];
            }
            return byteArray;
        }
        private int[] CreateTimeSeries(int arrayLength)
        {
            var timeArray = new int[arrayLength];
            for (int i = 0; i < arrayLength; i++)
            {
                int t = timeStep_ms * (i + 1);
                timeArray[i] = t;
            }
            return timeArray;
        }
        public void Dispose()
        {
            _apcq.Dispose();
        }
        /// <summary>
        /// Execute Python Script to Calculate New Open-Loop Control Signal
        /// </summary>
        public bool ExecutePythonNewThrowCalculation()
        {
            var pythonScriptPath = @"C:\Users\gardn\source\repos\MarinoArm\Python\MarinoArm.py";
            var pythonExePath = @"C:\Users\gardn\source\repos\MarinoArm\Python\venv\Scripts\python.exe";
            var exitCodeWasZero = RunPythonScript.RunCommand(pythonScriptPath, pythonExePath);
            return exitCodeWasZero;
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
        public ThrowData LoadCommandDataFromJson()
        {
            var jsonData = new ThrowData();
            jsonData.ReadJsonFile(readableJsonFilePath);
            var intArray = ConvertFloatArrayToIntArray(jsonData.Data.Shoulder.Cmd);
            var timeArray = CreateTimeSeries(intArray.Length);
            commandDataEvent(ConvertIntArraysToPointList(timeArray, intArray));
            return jsonData;
        }
        /// <summary>
        /// Loads the shoulder reference data from the python json
        /// </summary>
        private void LoadReferenceDataFromJson()
        {
            var jsonData = new ThrowData();
            jsonData.ReadJsonFile(readableJsonFilePath);
            var intArray = ConvertFloatArrayToIntArray(jsonData.Data.Shoulder.Ref);
            var timeArray = CreateTimeSeries(intArray.Length);
            referenceDataEvent(ConvertIntArraysToPointList(timeArray, intArray));
        }
        private int OpenLoopStaticOutput(double ref_deg)
        {
            double Kol = 165.0;
            double outMin = 80;
            double outMax = 200;
            int output = (int)(outMin + (Kol - outMin) * Math.Sin(ref_deg * 3.1416 / 180.0) - 5);
            if (output < outMin & output >=0)
            {
                return (int)outMin;
            }
            else if (output > outMax)
            {
                return (int)outMax;
            }
            else
            {
                return output;
            }
        }
    
        #endregion

        #region Unused Methods
        //listenAndUpdate Method
        void listenAndUpdate(SerialPort port)
        {
            char inChar;
            const int CODE_LENGTH = 10, RECEIPT_LENGTH = 6;
            int receiptFlag = 0;
            int codeFlag = 0;

            while (port.BytesToRead > 0)// & !codeFlag & !receiptFlag)
            {
                inChar = (char)port.ReadChar();
                if (inChar == '\r')
                {
                    if (inString.Length < 2)
                    {
                        inString = "";
                    }
                    else if (inString.Substring(0, 2) == "tx" & inString.Length == CODE_LENGTH)
                    {
                        codeFlag = 1;
                    }
                    else if (inString.Substring(0, 2) == "rx" & inString.Length == RECEIPT_LENGTH)
                    {
                        receiptFlag = 1;
                    }
                    else
                    {
                        inString = "";
                    }
                }

                else if (inChar != '\n' | inString.Length < 2)
                {
                    inString += inChar;
                }


                //Incoming Code Detected
                if (codeFlag == 1)
                {
                    if (showDiag)
                    {
                        Console.WriteLine("Incoming code detected:  " + inString);
                    }
                    inString = inString.Substring(2);
                    string locationString = inString.Substring(0, 4);
                    if (locationString[3] == '_')
                    {
                        locationString = locationString.Substring(0, 3);
                    }
                    //Serial.println("location String: " + locationString);
                    string stepString = inString.Substring(4);
                    //Serial.println("Step String: " + stepString);
                    //Serial.println("Size String: " + (String)sizeof(states));


                    inString = "";
                    codeFlag = 0;
                }



                //Incoming Reply from Outgoing Command Detected: reset send flag
                if (receiptFlag == 1)
                {
                    if (showDiag)
                    {
                        Console.WriteLine("Receipt code detected:  " + inString);
                    }

                    inString = "";
                    receiptFlag = 0;
                }

            }//while
        }//END listenAndUpdate

        public void ReadFastData(SerialPort port, string message, int[] arduinoData)
        {
            int i = 0;

            if (message == "HELLO")
            {
                isConnected = true;
                port.Write("READY\n");
                Console.WriteLine("C# heard Ardy's Hello");
                message = "";
            }
            else if ((isConnected & message.Length > 0) || isStarted)
            {
                //Console.WriteLine(message);
                if (message == "START")
                {
                    stopWatch.Start();
                    Console.WriteLine("Ardy is sending data... Now!");
                    isStarted = true;
                    i = 0;
                }
                else if (message == "END" || i >= 1000)
                {
                    isStarted = false;
                    stopWatch.Stop();
                    Console.WriteLine("Ardy just stopped sending data");
                    Console.WriteLine("Ardy sent: " + arduinoData.Length.ToString() + " data points");
                    Console.WriteLine("StopWatch: " + stopWatch.ElapsedMilliseconds.ToString() + "msec");

                }
                else if (isStarted)
                {
                    int inInt = port.ReadByte();
                    arduinoData[i] = inInt;
                    i++;
                    //Console.WriteLine(inInt);
                }


                /*port.Write(message + '\n');
                if (message.Substring(0, 1) == "T" | 1==0)
                {
                    Console.WriteLine(message);
                }*/
                message = "";
            }

        }

        #endregion
    }
}