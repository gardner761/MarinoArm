using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SerialLibrary
{
    public static class ArrayConverter
    {
        public static int[] ConvertFloatArrayToIntArray(float[] inArray)
        {
            int[] outArray = new int[inArray.Length];
            int i = 0;
            foreach (float item in inArray)
            {
                outArray[i] = (int)Math.Round(item);
                i++;
            }
            return outArray;
        }
        public static List<Point> ConvertIntArraysToPointList(int[] timeArray, int[] intArray)
        {
            var listData = new List<Point>();
            for (int i = 0; i < intArray.Length; i++)
            {
                Point p = new Point(timeArray[i], intArray[i]);
                listData.Add(p);
            }
            return listData;
        }
        public static byte[] ConvertIntArrayToByteArray(int[] intArray)
        {
            var L = intArray.Length;
            byte[] byteArray = new byte[L];
            for (int i = 0; i < L; i++)
            {
                byteArray[i] = (byte)intArray[i];
            }
            return byteArray;
        }
    }
}
