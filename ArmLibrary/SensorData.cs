using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArmLibrary
{
    public class SensorData
    {
        public struct DataStruct
        {
            public int Size { get; set; }
            public int[] ShoulderArray { get; set; }
        }

        public DataStruct Data;

        public SensorData()
        {
            Data.ShoulderArray = new int[]{ 1,13,33,77,3,13,19};
        }
        

        public void WriteJSONfile(string filepath)
        {
            //   @"c:\movie.json"
            var json_serializer = new JavaScriptSerializer();
            string jsonstring = json_serializer.Serialize(Data);
            File.WriteAllText(filepath, jsonstring);
        }


    }
}
