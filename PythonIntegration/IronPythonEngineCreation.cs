using IronPython.Hosting;
using IronPython.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PythonIntegration
{
    public class IronPythonEngineCreation
    {

        #region Defines

        

        #endregion
        public IronPythonEngineCreation()
        {
            TryIronPython();
            //run_cmd();
            //var pythonScriptPath = @"C:\Users\gardn\PycharmProjects\VisionTest\KuriousCleanup.py";
            //var pythonExePath = @"C:\Users\gardn\PycharmProjects\VisionTest\venv\Scripts\python.exe";
            //Task pythonScriptTask = ExecuteAsync(pythonScriptPath, pythonExePath);

   
        }














        static void TryIronPython()
        {
            try
            {
                // 1) Create Engine
                var engine = Python.CreateEngine();
                var script = @"C:\Users\gardn\PycharmProjects\VisionTest\CSharpTest.py";

                // 2) Provide script and arguments
                engine.ExecuteFile(script);
                
                var source = engine.CreateScriptSourceFromFile(script);
                var scope = engine.CreateScope();
                var ops = engine.Operations;
                var argv = new List<string>();
                argv.Add("");
                argv.Add("arg1");
                argv.Add("arg2");
                
                
                // 3) Execute
                source.Execute(scope);
                int j = scope.GetVariable("h");
                Console.WriteLine(j);
                IEnumerable<string> varList = scope.GetVariableNames();
                foreach (string item in varList)
                {
                    Console.WriteLine(item);
                }

                //var pythonType = scope.GetVariable("h");
                //dynamic instance = ops.CreateInstance(pythonType);
                //engine.GetSysModule().SetVariable("argv", argv);

                // 4) Output redirect
                /*
                var eIO = engine.Runtime.IO;

                var errors = new MemoryStream();
                eIO.SetErrorOutput(errors, Encoding.Default);

                var results = new MemoryStream();
                eIO.SetOutput(results, Encoding.Default);
                */

            }

            catch (Exception ex)
            {
                Console.WriteLine("Oops! There was an exception while " +
                   "running the script: " + ex.Message);
            }
        }
    }
}
