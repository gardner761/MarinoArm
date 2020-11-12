using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;


namespace SerialLibrary
{
    //
    class RobotArmProtocol
    {
        private string inString;
        private bool showDiag;
        bool isConnected;
        bool isStarted;
        Stopwatch stopWatch;

        //public void run(port)

        bool listenAndCheck(SerialPort port, string checkString)
        {
            char inChar;
            bool isEqual = false;
            while (port.BytesToRead > 0)// & !codeFlag & !receiptFlag)
            {
                inChar = (char)port.ReadChar();
                if (inChar == '\n')
                {
                    /*if(showDiag)
                    {
                        Console.WriteLine(inString);
                    }*/
                    if (inString.Equals(checkString))
                    {
                        isEqual = true;
                    }
                    inString = "";
                    break;
                }
                else
                {
                    inString = inString + inChar;
                }
            }//while

            if (isEqual)
            {
                isEqual = false;
                return true;
            }
            else
            {
                return false;
            }

        }

        public void listenAndEcho(byte inByte)
        {
            char inChar = (char)inByte;
            if (inChar == '\n')
            {
                if (inString.Substring(0, 1) == "T")
                {
                    Console.WriteLine(inString);
                }
                //port.Write(inString + '\n');
                inString = "";
            }
            else
            {
                inString = inString + inChar;
            }
        }

        //listenAndUpdate Method
        void listenAndUpdate(SerialPort port)
        {
            char inChar;
            const int CODE_LENGTH = 10, RECEIPT_LENGTH = 6;
            int receiptFlag = 0;
            int codeFlag = 0;

            while (port.BytesToRead > 0)// & !codeFlag & !receiptFlag)
            {
                inChar = (char)port.ReadChar();
                if (inChar == '\r')
                {
                    if (inString.Length < 2)
                    {
                        inString = "";
                    }
                    else if (inString.Substring(0, 2) == "tx" & inString.Length == CODE_LENGTH)
                    {
                        codeFlag = 1;
                    }
                    else if (inString.Substring(0, 2) == "rx" & inString.Length == RECEIPT_LENGTH)
                    {
                        receiptFlag = 1;
                    }
                    else
                    {
                        inString = "";
                    }
                }

                else if (inChar != '\n' | inString.Length < 2)
                {
                    inString += inChar;
                }


                //Incoming Code Detected
                if (codeFlag == 1)
                {
                    if (showDiag)
                    {
                        Console.WriteLine("Incoming code detected:  " + inString);
                    }
                    inString = inString.Substring(2);
                    string locationString = inString.Substring(0, 4);
                    if (locationString[3] == '_')
                    {
                        locationString = locationString.Substring(0, 3);
                    }
                    //Serial.println("location String: " + locationString);
                    string stepString = inString.Substring(4);
                    //Serial.println("Step String: " + stepString);
                    //Serial.println("Size String: " + (String)sizeof(states));


                    inString = "";
                    codeFlag = 0;
                }



                //Incoming Reply from Outgoing Command Detected: reset send flag
                if (receiptFlag == 1)
                {
                    if (showDiag)
                    {
                        Console.WriteLine("Receipt code detected:  " + inString);
                    }

                    inString = "";
                    receiptFlag = 0;
                }

            }//while
        }//END listenAndUpdate

        public void Read(SerialPort port, string message)
        {

            //Console.WriteLine("Uno says: " + message);
            if (message == "HELLO")
            {
                isConnected = true;
                port.Write("READY\n");
                Console.WriteLine("C# heard Uno's Hello");
                message = "";
            }
            else if ((isConnected & message.Length > 0) || isStarted)
            {
                //Console.WriteLine(message);
                if (message == "START")
                {
                    stopWatch.Start();
                    Console.WriteLine("Uno is sending data... Now!");
                    isStarted = true;
                    i = 0;
                }
                else if (message == "END" || i >= 1000)
                {
                    isStarted = false;
                    stopWatch.Stop();
                    Console.WriteLine("Uno just stopped sending data");
                    Console.WriteLine("Uno sent: " + UnoData.Length.ToString() + " data points");
                    Console.WriteLine("StopWatch: " + stopWatch.ElapsedMilliseconds.ToString() + "msec");

                }
                else if (isStarted)
                {
                    int inInt = port.ReadByte();
                    UnoData[i] = inInt;
                    i++;
                    //Console.WriteLine(inInt);
                }


                /*port.Write(message + '\n');
                if (message.Substring(0, 1) == "T" | 1==0)
                {
                    Console.WriteLine(message);
                }*/
                message = "";
            }

        }

    }
}

