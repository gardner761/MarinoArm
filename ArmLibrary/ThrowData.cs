using System.IO;
using System.Web.Script.Serialization;

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
            public JointStruct Shoulder;
            public JointStruct Elbow;
        }
        public struct JointStruct
        {
            public float[] Cmd { get; set; }
            public float[] Est { get; set; }
            public float[] Ref { get; set; }
            public int[] Sensor { get; set; }
        }

        #endregion

        #region Constructor
        public ThrowData()
        {
            //Data.Shoulder.Sensor = new int[] { 1, 13, 33, 77, 3, 13, 19 };
            //Data.Elbow.Sensor = new int[] { 77, 77, 77, 77, 77, 77, 77 };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the trial number to zero, sets the sampling frequency, and Initializes the Json file to be used by Python for the first throw
        /// </summary>
        /// <param name="path"></param>
        public void WriteFirstThrowDataToJson(string path)
        {
            Data.TrialNumber = 0;
            Data.SamplingFrequency = GlobalVariables.SAMPLING_FREQUENCY;
            WriteDataToJsonFile(path);
        }

        /// <summary>
        /// Writes the throw data to the Json file to be used by Python for the next throw
        /// </summary>
        /// <param name="path"></param>
        public void WriteDataToJsonFile(string path)
        {
            var json_serializer = new JavaScriptSerializer();
            string jsonstring = json_serializer.Serialize(Data);
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