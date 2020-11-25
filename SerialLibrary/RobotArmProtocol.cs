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
using System.IO;
using System.Windows.Forms;

// TODO - work on time series populating json at initialization and incorporate into ThrowData

// TODO - elbow data integration, 

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
 
        static int arraySize=GlobalVariables.ARRAY_SIZE; //1100 is close to the max for Unos, not sure about Megas
        int timeStep_ms = 1000 / GlobalVariables.SAMPLING_FREQUENCY;
       
        
        System.Timers.Timer timer;

        private ThrowType throwTypeRequested;

        #endregion

        #region Properties

        private ThrowData throwData;
        public ThrowData ThrowData
        {
            get 
            {
                return throwData;
            }
            private set
            {
                throwData = value;
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

        private States state;

        public States State
        {
            get { return state; }
            private set { state = value; }
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
            State = States.OnStartup;
        }

        #endregion

        #region State Machine
        private void ChangeStateTo(States nextstate)
        {
            State = nextstate;
            _stepNumber = 0;
            string message = State.ToString();
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

            switch (State)
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
                            ThrowData = new ThrowData();
                            ThrowData.WriteFirstThrowDataToJson(CSHARP_JSON_FILEPATH); //Initialize ThrowData
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
                            ThrowRequested = false;
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
                // TODO - make throw start at 0deg
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
                            filePath = SAVED_PYTHON_JSON_FILEPATH;
                        }
                        else
                        {
                            filePath = PYTHON_JSON_FILEPATH;
                        }

                        try
                        {
                            ThrowData = LoadThrowDataFromJson(filePath);
                            ChangeStateTo(States.Sending);
                        }
                        catch (FileNotFoundException e)
                        {
                            if (throwTypeRequested == ThrowType.Saved)
                            {
                                MessageBox.Show("You must save a throw before using this mode");
                                ChangeStateTo(States.Idle);
                            }
                            else
                            {
                                MessageBox.Show(e.Message);
                                ChangeStateTo(States.Error);
                            }
                            ThrowCtr--;
                        }                        
          
                        break;
                    }
                case States.Sending:
                    {
                        if (throwTypeRequested != ThrowType.Calculated)
                        {
                            ThrowData.Data.TrialNumber = ThrowCtr;
                        }

                        if (ThrowData.Data.TrialNumber != ThrowCtr)
                        {
                            errorMsg = $"Error: mismatch between internal throw counter and python counter: {ThrowCtr} to {ThrowData.Data.TrialNumber}";
                            ChangeStateTo(States.Error);
                        }

                        //Wipe the sensor data prior to starting the throw
                        ThrowData.Data.Shoulder.Sensor = null;
                        ThrowData.Data.Elbow.Sensor = null;

                        //Send throwData to arduino
                        if (_stepNumber == 0) 
                        {
                            masp.CheckString = "START";
                            masp.IsDetected = false;
                            apcq.SwitchConsumerActionTo(masp.ListenAndCheck);
                            masp.SendNewThrowDataToArdy(ThrowData);
                            _stepNumber = 10;
                        }

                        //Wait for Arduino to reply with "START", then start adding new data to Byte list
                        if (_stepNumber == 10 & masp.IsDetected) 
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
                            ThrowData.Data.Shoulder.Sensor = masp.ShoulderSensorData.ToArray();
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
                            ThrowData.Data.DateExecuted = DateTime.Now;
                            ThrowData.WriteDataToJsonFile(CSHARP_JSON_FILEPATH);
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

        public void SavePythonJson()
        {
            ThrowData.WriteDataToJsonFile(GlobalVariables.SAVED_PYTHON_JSON_FILEPATH);
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

        // TODO - look at the usage of this function, use it to populate initial CSharp Json, but then all time series shoudld be referenced from Json after that.
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