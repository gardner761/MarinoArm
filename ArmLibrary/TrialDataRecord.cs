using System.IO;
using System.Web.Script.Serialization;

namespace ArmLibrary
{
    public class TrialDataRecord
    {
        #region Properties

        public DataStruct Data;

        
        public struct DataStruct
        {
            public int TrialCount { get; set; }
            public TrialDataStruct[] TrialArray;
        }

        
        
        public struct TrialDataStruct
        {
            public int[] time { get; set; }
            public int[] a { get; set; }
            public int[] r { get; set; }
            public int[] u { get; set; }
            public int[] e { get; set; }
            public int[] y { get; set; }
            public int norme { get; set; }
            public int TrialNumber { get; set; }
        }

        #endregion

        #region Constructor
        public TrialDataRecord()
        {
            Data.TrialCount = 0;
        }

        #endregion

        #region Json Methods
        public void WriteJsonFile(string filepath)
        {
            //   @"c:\movie.json"
            var json_serializer = new JavaScriptSerializer();
            string jsonstring = json_serializer.Serialize(Data);
            File.WriteAllText(filepath, jsonstring);
        }

        public void ReadJsonFile(string filepath) //filepath should be of format: @"C:\movie.json"
        {
            string json = File.ReadAllText(filepath);
            var json_serializer = new JavaScriptSerializer();
            Data = json_serializer.Deserialize<DataStruct>(json);
        }

        #endregion
    }
}
