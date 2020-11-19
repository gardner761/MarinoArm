using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArmLibrary
{
    public static class GlobalVariables
    {
        public const int SAMPLING_FREQUENCY = 100;
        /// <summary>
        /// C# writes to this file
        /// </summary>
        public const string CSHARP_JSON_FILEPATH = @"C:\Users\gardn\source\repos\MarinoArm\Json\DataFromCSharp.json";
        /// <summary>
        /// Python writes to this file
        /// </summary>
        public const string PYTHON_JSON_FILEPATH = @"C:\Users\gardn\source\repos\MarinoArm\Json\DataFromPython.json";
        /// <summary>
        /// Stored python filepath
        /// </summary>
        public const string SAVED_PYTHON_JSON_FILEPATH = @"C:\Users\gardn\source\repos\MarinoArm\Json\Saved\DataFromPython_saved.json";
    }
}
