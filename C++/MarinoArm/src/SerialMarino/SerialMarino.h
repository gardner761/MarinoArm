/*
  SerialMarino.h - Library for board to board serial transmission code
  Created by Jake Gardner, March 24, 2020
  Released into the public domain.
*/

#ifndef SerialMarino_h
#define SerialMarino_h

#include "Arduino.h"
#include "Timer.h"


class SerialMarino
{
  public:
    SerialMarino(HardwareSerial* ptrSerial);
    void begin(long baud);
    boolean listenAndCheck(String checkString); //
    boolean listenForInt(int* passThruInt);
    boolean collectNewData(byte dataArray[], int arraySize);
    void writeByte(byte b);
	void sendMessage(String msg); //
    bool isError; //state bits
    const static int arraySize = 100;
	
  protected:
    HardwareSerial* inSerial;
	Timer timerSend;
	bool showDiag;
    char inChar;
	String inString,locationString,stepString,sentString;
    int queueIndex, sendIndex;
	bool codeFlag,receiptFlag,sendNextFlag,resendFlag;
	const int CODE_LENGTH=10,RECEIPT_LENGTH=6;
    byte buffArray[arraySize];
};

#endif