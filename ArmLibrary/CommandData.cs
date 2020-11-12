using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArmLibrary
{
    public class CommandData
    {
        public struct DataStruct
        {
            public int Size { get; set; }
            public int[] CmdArray { get; set; }
            public int[] ShoulderArray { get; set; }
        }

        public DataStruct Data;

        public CommandData()
        {
        }

        public void ConvertJSONs(string json)
        {
            var json_serializer = new JavaScriptSerializer();
            Data = json_serializer.Deserialize<DataStruct>(json);
            //Data.Size = Data.CmdArray.Length;
            Console.WriteLine(Data.Size);
        }
    }
}
