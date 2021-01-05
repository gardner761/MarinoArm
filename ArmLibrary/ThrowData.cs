using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using Newtonsoft.Json;

namespace ArmLibrary
{
    public class ThrowData
    {
        #region Properties

        public DataStruct Data;
        public struct DataStruct
        {
            public int TrialNumber { get; set; } //read by Python Script; it tells the gILC to start new (assign zeros to estimation)
            public int SamplingFrequency { get; set; }
            public DateTime DateCalculated { get; set; }
            public DateTime DateExecuted { get; set; }
            public int ArraySize { get; set; }
            public JointStruct Shoulder;
            public JointStruct Elbow;
            
        }
        public struct JointStruct
        {
            public float[] Cmd { get; set; }
            public float[] Est { get; set; }
            public float[] Ref { get; set; }
            public int[] Sensor { get; set; }
            public float[] Time { get; set; }
        }

        #endregion

        #region Constructor
        public ThrowData()
        {
            //Data.Shoulder.Sensor = new int[] { 1, 13, 33, 77, 3, 13, 19 };
            //Data.Elbow.Sensor = new int[] { 77, 77, 77, 77, 77, 77, 77 };
            //Data.DateCalculated = DateTime.Now;
            //Data.DateExecuted = Data.DateCalculated;
        }

        #endregion

        #region Methods

        /// <summary>
            /// Sets the trial number to zero, sets the sampling frequency, and Initializes the Json file to be used by Python for the first throw
            /// </summary>
            /// <param name="path"></param>
        public void WriteFirstThrowDataToJson(string path, float[] time)
        {
            Data.TrialNumber = 0;
            Data.ArraySize = GlobalVariables.ARRAY_SIZE;
            Data.SamplingFrequency = GlobalVariables.SAMPLING_FREQUENCY;
            Data.Shoulder.Time = time;
            Data.Elbow.Time = time;
            WriteDataToJsonFile(path);
        }

        /// <summary>
        /// Writes the throw data to the Json file to be used by Python for the next throw
        /// </summary>
        /// <param name="path"></param>
        public void WriteDataToJsonFile(string path)
        {
            var settings = new JsonSerializerSettings { DateFormatString = "yyyy-MM-ddTHH:mm:ss.ffffffZ" };
            var jsonstring = JsonConvert.SerializeObject(Data, settings);
            File.WriteAllText(path, jsonstring);
        }

        /// <summary>
        /// Reads Json File and stores throw Data
        /// </summary>
        /// <param name="path"></param>
        public void ReadJsonFile(string path) //filepath should be of format: @"C:\movie.json"
        {
            string json = File.ReadAllText(path);
            var json_serializer = new JavaScriptSerializer();
            Data = json_serializer.Deserialize<DataStruct>(json);
        }
        

        #endregion
    }
}