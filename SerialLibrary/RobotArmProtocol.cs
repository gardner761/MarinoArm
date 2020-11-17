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
    // TODO - Make python scripting async

    public class RobotArmProtocol
    {
        #region Defines

        public delegate void StateChangedEvent(string message);
        public event StateChangedEvent stateChangedEvent;

        public delegate void ReferenceDataEvent(List<Point> data);
        public event ReferenceDataEvent referenceDataEvent;
        public delegate void CommandDataEvent(List<Point> data);
        public event CommandDataEvent commandDataEvent;

        public MarinoArmSerialProcessor masp;
        private String portName;
        
        private string inString;
        private const bool showDiag=true;

        public bool CalculateNewThrowRequested { get; set; }

        bool isStarted;
        bool isConnected;
        public int refreshPlotCount;

        Stopwatch stopWatch;
        AsyncProducerConsumerQueue<byte> apcq;
        public States state; 
 
        private static double throwDuration_sec = 2.0;
        static int arraySize=(int)((double)GlobalVariables.SAMPLING_FREQUENCY*throwDuration_sec) + 1; //1100 is close to the max for Unos, not sure about Megas
        int timeStep_ms = 1000 / GlobalVariables.SAMPLING_FREQUENCY;
        private ThrowData throwData;
        
        bool startingWithSavedPythonFile = false;
        System.Timers.Timer timer;

        #endregion

        #region Properties

        private int throwCtr;
        public int ThrowCtr
        {
            get { return throwCtr; }
            set { throwCtr = value; }
        }

        public bool ThrowRequested { get; set; }

        public SerialClient SerialClient { get; set; }

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

        public RobotArmProtocol(String portName)
        {
            // Stores the port name to be used
            this.portName = portName;
            SerialClient = new SerialClient(portName);
            SerialClient.connectionMadeEvent += SerialClient_ConnectionMadeEvent;

            // Initializes the begging state of the state machine
            state = States.OnStartup;
        }

        #endregion

        #region State Machine
        private void ChangeStateTo(States nextstate)
        {
            state = nextstate;
            _stepNumber = 0;
            string message = state.ToString();
            Console.WriteLine($"RAP State is: {message}");
            if (stateChangedEvent != null)
            {
                stateChangedEvent(message);
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (!isCurrentlyScanning)
            {
                UpdateStateMachine();
            }
            //Console.WriteLine("Timer Elapsed");
        }

        public void SerialClient_ConnectionMadeEvent()
        {
            //Starts the periodic clock to execute the UpdateStateMachine method
            timer = new System.Timers.Timer(200); //1000 msec
            timer.Elapsed += timer_Tick;
            timer.Start();
        }

        bool isCurrentlyScanning = false;
        int _stepNumber = 0;

        public void UpdateStateMachine()
        {
            isCurrentlyScanning = true;

            switch (state)
            {
                case States.OnStartup:
                    {
                        if (SerialClient.SerialPort.IsOpen)
                        {
                            // Instantiate masp and the apcq
                            masp = new MarinoArmSerialProcessor(SerialClient.SerialPort, arraySize, timeStep_ms);
                            masp.wordDetectedEvent += UpdateStateMachine;
                            masp.CheckString = "HELLO";
                            masp.IsDetected = false;
                            apcq = new AsyncProducerConsumerQueue<byte>(masp.ListenAndCheck);
                            SerialClient.addByteToQueueEvent += apcq.Produce;

                            // Initialize data and write the first throw json
                            ThrowCtr = 0;
                            throwData = new ThrowData();
                            throwData.WriteFirstThrowDataToJson(writeToJsonFilePath); //Initialize ThrowData
                            Console.WriteLine("Initializing First Throw Json File");
                            masp.SendMessageToArdy("HELLO");
                            ChangeStateTo(States.Initialized);
                        }

                        break;
                    }
                case States.Initialized:
                    {
                        //Wait to receive Hello from Arduino, then send arraySize to arduino
                        if (masp.IsHelloReceived & _stepNumber == 0)
                        {
                            masp.IsHelloReceived = false;
                            masp.IsDetected = false;
                            masp.SendMessageToArdy(arraySize.ToString());
                            masp.CheckString = arraySize.ToString();
                            apcq.SwitchConsumerActionTo(masp.ListenAndCheck);
                            ThrowRequested = false;
                            _stepNumber = 1;
                        }

                        //Wait to receive echo of arraySize from Arduino
                        if (masp.IsDetected & _stepNumber==1)
                        {
                            masp.IsDetected = false;
                            _stepNumber = 0;
                            ChangeStateTo(States.Idle);
                        }

                        break;
                    }
                case States.Idle:
                    {
                        if (ThrowRequested)
                        {
                            ThrowCtr++;
                            CalculateNewThrowRequested = true;
                            ChangeStateTo(States.TaskPlanning);
                        }

                        break;
                    }
                case States.TaskPlanning:
                    {
                        if (CalculateNewThrowRequested)
                        {
                            ChangeStateTo(States.Calculating);
                        }

                        break;
                    }
                case States.Calculating:
                    {
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


                            ChangeStateTo(States.Starting);
                        }
                        else //Python did not execute successfully
                        {
                            Console.WriteLine("Python did not execute successfully, debugging needed");
                            ChangeStateTo(States.Error);
                           
                        }
                        
                        break;
                    }
                case States.Starting:
                    {

                        if (_stepNumber == 0) //Send throwData to arduino
                        {
                            masp.CheckString = "START";
                            masp.IsDetected = false;
                            apcq.SwitchConsumerActionTo(masp.ListenAndCheck);
                            masp.SendNewThrowDataToArdy(throwData);
                            _stepNumber = 10;
                        }

                        if (_stepNumber == 10 & masp.IsDetected) //Wait for Arduino to reply with "START", then start adding new data to Byte list
                        {
                            masp.ShoulderSensorData.Clear();
                            masp.IsDetected = false;
                            masp.CheckString = "END";
                            apcq.SwitchConsumerActionTo(masp.AddByteToList);
                            _stepNumber = 0;
                            ChangeStateTo(States.Receiving);
                        }

                        break;
                    }
                case States.Receiving: //Reading Data from Arduino
                    {
                        if (masp.IsFinished) //Waiting to read the word "END" from Arduino
                        {
                            masp.SendMessageToArdy("RECEIVED");
                            masp.IsFinished = false;
                            apcq.SwitchConsumerActionTo(masp.Listen);
                            throwData.Data.Shoulder.Sensor = masp.ShoulderSensorData.ToArray();
                            ChangeStateTo(States.Done);
                        }
                        break;
                    }
                case States.Done:
                    {
                        if (true)
                        {
                            apcq.SwitchConsumerActionTo(masp.ListenAndCheck);
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

            isCurrentlyScanning = false;
        }

        #endregion

        #region Helper Methods

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
            SerialClient.Dispose();
            apcq.Dispose();
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

        public ThrowData LoadCommandDataFromJson()
        {
            var jsonData = new ThrowData();
            jsonData.ReadJsonFile(readableJsonFilePath);
            var intArray = ArrayConverter.ConvertFloatArrayToIntArray(jsonData.Data.Shoulder.Cmd);
            var timeArray = CreateTimeSeries(intArray.Length);
            commandDataEvent(ArrayConverter.ConvertIntArraysToPointList(timeArray, intArray));
            return jsonData;
        }
        /// <summary>
        /// Loads the shoulder reference data from the python json
        /// </summary>
        private void LoadReferenceDataFromJson()
        {
            var jsonData = new ThrowData();
            jsonData.ReadJsonFile(readableJsonFilePath);
            var intArray = ArrayConverter.ConvertFloatArrayToIntArray(jsonData.Data.Shoulder.Ref);
            var timeArray = CreateTimeSeries(intArray.Length);
            referenceDataEvent(ArrayConverter.ConvertIntArraysToPointList(timeArray, intArray));
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