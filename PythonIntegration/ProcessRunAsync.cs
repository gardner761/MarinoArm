using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonIntegration
{
    public static class ProcessRunAsync
    {
        public static string output;
        public static Task RunAsync(this Process process)
        {
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => tcs.TrySetResult(null);
            // not sure on best way to handle false being returned
            if (!process.Start()) tcs.SetException(new Exception("Failed to start process."));
            output = process.StandardOutput.ReadToEnd();
            //Console.WriteLine(output);
            //process.WaitForExit();
            
            return tcs.Task;
        }
    }
}
