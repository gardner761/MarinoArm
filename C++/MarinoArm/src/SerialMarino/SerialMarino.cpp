/*
  SerialMarino.cpp - Library for C# to board serial transmissions code
  Created by Jake Gardner, March 24, 2020
  Released into the public domain.
*/

#include "SerialMarino.h"
#include "Arduino.h"
#include "Timer.h"

const int BUFF_SIZE = 11;

//Constructor
SerialMarino::SerialMarino(HardwareSerial* ptrSerial)
{
	inSerial = ptrSerial;
	showDiag = false;  //change this parameter if you want it on
	queueIndex = 0;
	sendIndex = 0;
	isError = 0;
	receiptFlag = 0;
	codeFlag = 0;
	sendNextFlag = 1;
	timerSend.reset();
	inString = "";
}

void SerialMarino::begin(long baud)
{
	inSerial->begin(baud);
}

boolean SerialMarino::listenForInt(int* passThruInt)
{
	codeFlag = false;
	while (inSerial->available())// & !codeFlag & !receiptFlag)
	{
		inChar = inSerial->read();
		if (inChar == '\n')
		{
			if (showDiag)
			{
				Serial.println("InString:  " + inString);
			}

			codeFlag = 1;
			*passThruInt = inString.toInt();
			break;
		}
		else
		{
			inString += inChar;
		}
	}
	//Incoming Code Detected
	if (codeFlag)
	{
		inString = "";
		codeFlag = false;
		return true;
	}
	else
	{
		return false;
	}
}//END listenForInt

//listenAndCheck Method: listen for a string argument and return true when it is found at the end of a line
boolean SerialMarino::listenAndCheck(String checkString)
{
	codeFlag = false;
	while (inSerial->available())// & !codeFlag & !receiptFlag)
	{
		inChar = inSerial->read();
		if (inChar == '\n')
		{
			if (showDiag)
			{
				Serial.println(inString);
			}
			if (inString.endsWith(checkString))
			{
				codeFlag = 1;
			}
			else
			{
				inString = "";
			}
			break;
		}
		else
		{
			inString += inChar;
		}
	}
	//Incoming Code Detected
	if (codeFlag)
	{
		inString = "";
		codeFlag = 0;
		return true;
	}
	else
	{
		return false;
	}
}//END listenAndCheck

boolean SerialMarino::collectNewData(byte dataArray[],int arraySize)
{
	codeFlag = false;
	static int j = 0;
	//int arraySize = int(sizeof(dataArray) / sizeof(dataArray[0]));

	while (Serial.available())
	{
		inChar = Serial.read();
		byte inByte = byte(inChar);

		



		if (j < arraySize)
		{
				//initializing the array
				if (j == 0)
				{
					for (int i = 0; i < arraySize; i++)
					{
						dataArray[i] = 0;
					}
				}

				dataArray[j] = inByte;

				if (showDiag & j == arraySize - 1)
				{
					Serial.print("CollectNewData Last Data Byte:  j = ");
					Serial.print(j);
					Serial.print(" and last value is: ");
					Serial.println(dataArray[j]);
				}
				j++;
		}
		else if (inChar == '\n' & inString.endsWith("END") & j==arraySize)
		{
			codeFlag = true;
			inString = "";
			j = 0;
			break;
		}
		else
		{
			//Building and modifiying inString to be exactly 3 characters long, which is how many chars "END" has
			if (inString.length() >= 3)
			{
				inString = inString.substring(1, 3) + String(inChar);
			}
			else
			{
				inString = inString + inChar;
			}

			if (showDiag & j == arraySize)
			{
				Serial.println("Done Collecting, inString is: " + inString);
			}
		}
	}

	if (codeFlag)
	{
		return true;
	}
	else
	{
		return false;
	}
}

//Send Method
void SerialMarino::sendMessage(String msg)
{
	inSerial->print(msg + '\n');
}

void SerialMarino::writeByte(byte b)
{
	inSerial->write(b);
}
