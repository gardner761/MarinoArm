using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArmLibrary
{
    public static class GlobalVariables
    {
        // TODO - convert back
        public const int ARRAY_SIZE = 50; //326; 
        public const int SAMPLING_FREQUENCY = 100;
        /// <summary>
        /// C# writes to this file
        /// </summary>
        public const string CSHARP_JSON_FILEPATH = @"C:\Users\gardn\source\repos\MarinoArm\Json\temp\DataFromCSharp.json";
        /// <summary>
        /// Python writes to this file
        /// </summary>
        public const string PYTHON_JSON_FILEPATH = @"C:\Users\gardn\source\repos\MarinoArm\Json\temp\DataFromPython.json";
        /// <summary>
        /// Stored python filepath
        /// </summary>
        public const string SAVED_PYTHON_JSON_FILEPATH = @"C:\Users\gardn\source\repos\MarinoArm\Json\Saved\SavedDataFromCsharp.json";
    }
}
