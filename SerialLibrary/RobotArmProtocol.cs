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
    public class RobotArmProtocol
    {
        #region Defines

        public delegate void StateChangedEvent(string message);
        public event StateChangedEvent stateChangedEvent;

        public MarinoArmSerialProcessor masp;

        private const bool showDiag=true;

        public bool CalculateNewThrowRequested { get; set; }

        public int refreshPlotCount;

        AsyncProducerConsumerQueue<byte> apcq;
        public States state; 
 
        private static double throwDuration_sec = 2.0;
        static int arraySize=(int)((double)GlobalVariables.SAMPLING_FREQUENCY*throwDuration_sec) + 1; //1100 is close to the max for Unos, not sure about Megas
        int timeStep_ms = 1000 / GlobalVariables.SAMPLING_FREQUENCY;
        private ThrowData throwData;
        
        System.Timers.Timer timer;

        private ThrowType throwTypeRequested;

        #endregion

        #region Properties

        public ThrowData ThrowData
        {
            get 
            {
                return throwData;
            }
            private set
            {
            }
        }

        /// <summary>
        /// Selected by the UI
        /// </summary>
        public ThrowType ThrowTypeSelected { get; set; }

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
            Loading,
            Sending,
            Receiving,
            Done = 100,
            Error = 911
        }

        public enum ThrowType
        {
            Calculated,
            Saved,
            Rerun
        }

        #endregion

        #region Constructors

        public RobotArmProtocol()
        {
            //Instantiates SerialClient
            SerialClient = new SerialClient();
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
            timer = new System.Timers.Timer(50); //1000 msec
            timer.Elapsed += timer_Tick;
            timer.Start();
        }

        bool isCurrentlyScanning = false;
        int _stepNumber = 0;
        string errorMsg;
       

        /// <summary>
        /// Manages the state of the throw
        /// </summary>
        public async void UpdateStateMachine()
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
                            throwData.WriteFirstThrowDataToJson(WRITETO_JSON_FILEPATH); //Initialize ThrowData
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
                            throwTypeRequested = ThrowTypeSelected;
                            ChangeStateTo(States.TaskPlanning);
                        }

                        break;
                    }
                case States.TaskPlanning:
                    {
                        switch(throwTypeRequested)
                        {
                            case ThrowType.Calculated:
                                {
                                    ChangeStateTo(States.Calculating);
                                    break;
                                }
                            case ThrowType.Saved:
                                {
                                    ChangeStateTo(States.Loading);
                                    break;
                                }
                            case ThrowType.Rerun:
                                {
                                    ChangeStateTo(States.Sending);
                                    break;
                                }
                            default:
                                {
                                    errorMsg = "Error: no throw type was selected";
                                    ChangeStateTo(States.Error);
                                    break;
                                }
                        }

                        break;
                    }
                case States.Calculating:
                    {

                        bool pythonExecutedSuccessfully = await Task.Run(() => ExecutePythonNewThrowCalculation());

                        if (pythonExecutedSuccessfully)
                        {
                                ChangeStateTo(States.Loading);
                        }
                        else
                        {
                            errorMsg = "Python did not execute successfully, debugging needed";
                            ChangeStateTo(States.Error);
                        }
                        
                        break;
                    }
                case States.Loading: //Loading Json from Python
                    {
                        string filePath;
                        if(throwTypeRequested == ThrowType.Saved)
                        {
                            filePath = READABLE_SAVED_PYTHON_JSON_FILEPATH;
                        }
                        else
                        {
                            filePath = READABLE_JSON_FILEPATH;
                        }

                        throwData = LoadThrowDataFromJson(filePath);

                        if (throwTypeRequested == ThrowType.Saved)
                        {
                            throwData.Data.TrialNumber = ThrowCtr;
                        }

                        if (throwData.Data.TrialNumber != ThrowCtr)
                        {
                            errorMsg = $"Error: mismatch between internal throw counter and python counter: {ThrowCtr} to {throwData.Data.TrialNumber}";
                            ChangeStateTo(States.Error);
                        }
                        else
                        {
                            ChangeStateTo(States.Sending);
                        }

                        break;
                    }
                case States.Sending:
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
                            timer.Stop();
                            ChangeStateTo(States.Receiving);
                        }

                        break;
                    }
                case States.Receiving: //Receiving Data from Arduino
                    {
                        if (masp.IsFinished) //Waiting to read the word "END" from Arduino
                        {
                            masp.SendMessageToArdy("RECEIVED");
                            masp.IsFinished = false;
                            apcq.SwitchConsumerActionTo(masp.Listen);
                            throwData.Data.Shoulder.Sensor = masp.ShoulderSensorData.ToArray();
                            timer.Start();
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
                            throwData.WriteDataToJsonFile(WRITETO_JSON_FILEPATH);
                            ChangeStateTo(States.Idle);
                        }
                        break;
                    }
                case States.Error:
                    {
                        throw new Exception(errorMsg);
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
        public int[] CreateTimeSeries(int arrayLength)
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
            timer.Dispose();
        }

        /// <summary>
        /// Execute Python Script to Calculate New Open-Loop Control Signal
        /// </summary>
        private bool ExecutePythonNewThrowCalculation()
        {
            var pythonScriptPath = @"C:\Users\gardn\source\repos\MarinoArm\Python\MarinoArm.py";
            var pythonExePath = @"C:\Users\gardn\source\repos\MarinoArm\Python\venv\Scripts\python.exe";
            var exitCodeWasZero = RunPythonScript.RunCommand(pythonScriptPath, pythonExePath);
            return exitCodeWasZero;
        }

        public ThrowData LoadThrowDataFromJson(String readableJsonFilePath)
        {
            var jsonData = new ThrowData();
            jsonData.ReadJsonFile(readableJsonFilePath);
            return jsonData;
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
    }
}