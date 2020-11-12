using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonIntegration
{
    /// <summary> 
    /// used to execute python scripts
    /// </summary>
    public static class RunPythonScript 
                                        
    {
        /// <summary> 
        /// executes a python script by starting a new process and waiting for its exit
        /// </summary>
        public static bool RunCommand(string scriptPath, string pythonExePath)
        {
            using (var process = new Process())
            {
                // configure process
                process.StartInfo = new ProcessStartInfo(pythonExePath, scriptPath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    //Arguments = string.Format("{0} {1} {2}", scriptPath, 5, 113)
                };
                // start process
                process.Start();

                // do stuff with results
                string output = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Console.WriteLine($"{scriptPath} finished running at {process.ExitTime} with exit code {process.ExitCode}");
                var exitCodeWasZero = process.ExitCode == 0;
                if (!exitCodeWasZero)
                {
                    Console.WriteLine($"Error when running python script: {err}");
                }
                return exitCodeWasZero;
            };// dispose process

        }

        public static async Task ExecuteAsync(string executablePath, string pythonExePath)
        {
            using (var process = new Process())
            {
                // configure process
                process.StartInfo = new ProcessStartInfo(pythonExePath, executablePath)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = string.Format("{0} {1} {2}", executablePath, 5, 113)
                };
                // run process asynchronously
                Environment.SetEnvironmentVariable("xFromC", "13");
                await process.RunAsync();
                // do stuff with results
                //var x = Environment.GetEnvironmentVariable("x");
                Console.WriteLine($"x is: {process.StandardOutput}");
                Console.WriteLine($"Process finished running at {process.ExitTime} with exit code {process.ExitCode}");
            };// dispose process
        }

    }
}
