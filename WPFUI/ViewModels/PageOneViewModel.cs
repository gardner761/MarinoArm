using Caliburn.Micro;
using System;
using System.Windows;
using PythonIntegration;
using ArmLibrary;

namespace WPFUI.ViewModels
{
    class PageOneViewModel : Screen
    {
        #region Defines

        private String _processStateString;
        private Visibility _startButtonVisibility;

        #endregion

        #region Properties
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

        #region Start Button
        public bool CanStartButton()
        {
            return true;
        }
        public void StartButton() //Bound to UI button click
        {
            StartButtonVisibility = Visibility.Hidden;

            //1. Initialize ThrowData
            //      a. Write trial number equal to zero into JSON file for Python to Read as "first throw" indicator
            //      b. Write the sample frequency into the JSON file
            var throwData = new ThrowData();
            var writeToJsonFilePath = @"C:\Users\gardn\source\repos\MarinoArm\Python\DataFromCSharp.json";
            //throwData.WriteFirstThrowDataToJson(writeToJsonFilePath);

            //2. Execute Python Script to Calculate New Open-Loop Control Signal
            var pythonScriptPath =  @"C:\Users\gardn\source\repos\MarinoArm\Python\CSharpTest.py";
            //var pythonScriptPath = @"C:\Users\gardn\source\repos\MarinoArm\Python\testMe.py";
            var pythonExePath =     @"C:\Users\gardn\source\repos\MarinoArm\Python\venv\Scripts\python.exe";

            var pythonExecutedWithSuccess = RunPythonScript.RunCommand(pythonScriptPath, pythonExePath);


            //3. Get New Control Signal By Reading the Python-Generated JSON File
            var commandData = new ThrowData();
            var readableJsonFilePath = @"C:\Users\gardn\source\repos\MarinoArm\Python\DataFromPython.json";
            commandData.ReadJsonFile(readableJsonFilePath);
        }

        #endregion
    }
}