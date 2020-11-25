////////////////////////////////////////////////////////////////////////////
//MARINOARM (SLAVE)//
//This board is a slave controller
///////////////////////////////////////////////////////////////////////////

//Library Declarations
#include <Timer.h>
#include "src\SerialMarino\SerialMarino.h"
#include <Fir1fixed.h>

const bool isSkipMoveToInitPosStep = true;

bool connectionEstablished;
Timer timerMicro(true), timerMilli, timerSensor(true), timerDISP(true);

//Diagnostics
int thisScanTime = 0, maxScanTime = 0, ctr = 0, t = 0;
double totalScanTime, timeStart, timeStop, timeNow, lastTime;
float avgScanTime;
bool isMatch;
String inString = "";
double Kol; // variable to store the value coming from the sensor
double thetaDot;
int outVal;
int sensorPin = A0;    // select the input pin for the potentiometer
int valvePin = 22;      // select the pin for the LED
int outPin = 11;
int ledPin = 13;
int sensorDeg;
int zeroOffset;

int freqHz = 100;
SerialMarino serialM(&Serial);
int startPos = 54; //this is the first value of the reference signal
int isStepNumber;
float sensorThetaDot;
int lastReading;
bool isStill;
long clockTime;
int errDeg;
bool onTarget, closeToTarget;
long iSens;
int outMin = 80, outMax = 200;
bool isConnected;
bool flag, disp = false;
int stillSum;
bool isCalibration = false;
int arraySize; //updated after program runs, is provided by CSharp
const int sizer = 101;   //326; //this should be larger than what the arraySize value is
byte psiDataInArray[sizer]; //WARNING!!!!! CHANGE THIS BACK TO match the RAP arraySize
byte ipDataArray[sizer];
bool switchSolenoidDataArray[sizer];
bool switchOn;

//Calibration Data
//AnalogVal     Output psi
//50            n/a
//75            5
//80            7.5
//90            10.5
//100           13.1
//110           15.9
//125           19.7
//150           26.1
//200           39









void setup()
{
  serialM.begin(115200);
  pinMode(valvePin, OUTPUT);
  pinMode(outPin, OUTPUT);
  pinMode(ledPin, OUTPUT);
  analogWrite(outPin, 0);
  digitalWrite(valvePin, LOW);

  // INITIALIZING THE ROBOT SENSORS AND TRAVELLING TO START POSITION
  zeroOffset = GetZeroReading();
  //Serial.println(zeroOffset);


  //CONNECTING: Sending Hello and Waiting for one Back from
  //only does this one time
  if (!isConnected & !isCalibration)
  {
    timerMilli.set(0);
    while (!serialM.listenAndCheck("HELLO")){}
    serialM.sendMessage("HELLO");
    while (!serialM.listenForInt(&arraySize)) {}   // assigns the arraySize from C# message
    serialM.sendMessage(String(arraySize));
  }


  if(arraySize>sizer)
  {
    serialM.sendMessage("arraySize is larger than the alloted size in arduino, please fix in arduino code");
  }
  else
  {
  ChangeStepNumber(0);
  }

}










void loop()
{
  //STEP 0: initializing
  if (isStepNumber == 0)
  {
    clockTime = 1000000 / freqHz;
    timerSensor.set(clockTime / 50);
    timerMicro.set(clockTime);
    timerDISP.set(clockTime * 50);
    ChangeStepNumber(5);
  }








  //SENSOR READINGS
  //acquiring Sensor Data and Filtering
  if (timerSensor.runMetronome())
  {
    CalcSensorDeg(true);
  }
  // display
  flag = false;
  if (timerDISP.runMetronome())
  {
    flag = true;
    if (disp)
    {
      Serial.println("SensorDeg: " + String(sensorDeg));
    }
  }









  //STEP 5: Waiting for first display reading
  if (isStepNumber == 5)
  {
    if (flag)
    {
      if (isCalibration)
      {
        disp = true;
        ChangeStepNumber(30);

      }
      else
      {
        ChangeStepNumber(10);
        timerMilli.set(1500);
      }
    }
  }









  //STEP 10: Wait for New Throw Command from C#
  if (isStepNumber == 10)
  {
    if (serialM.listenAndCheck("NEWTHROW"))
    {
      ChangeStepNumber(20);
      timerMilli.set(150);
    }
    else
    {
      timerMilli.call();
      if (timerMilli.isDone)
      {
        timerMilli.set(1500);
        if(switchOn)
        {
          switchOn = false;
        }
        else
        {
          switchOn = true;
        }
        digitalWrite(LED_BUILTIN, switchOn);
      }
    }
  }








  //STEP 20: Receiving New P.S.I. Data for Throw

  if (isStepNumber == 20)
  {

    if (serialM.collectNewData(psiDataInArray, arraySize))
    {
      ChangeStepNumber(25);
    }
    else
    {
      timerMilli.call();
      if (timerMilli.isDone)
      {
        timerMilli.set(150);
        if(switchOn)
        {
          switchOn = false;
        }
        else
        {
          switchOn = true;
        }
        digitalWrite(LED_BUILTIN, switchOn);
      }
    }
  }






  //STEP 25: Converting Data for I/P Transducer and Switch Solenoid
  if (isStepNumber == 25)
  {
    convertToIPTransducer(); //populates the ipDataArray and switchSolenoidDataArray

    if(isSkipMoveToInitPosStep)
    {
      ChangeStepNumber(40);
    }
    else
    {
      ChangeStepNumber(30);
    }
  }







  //STEP 30: Bump to starting position
  if (isStepNumber == 30 & isStill & flag)
  {
    if (disp)
    {
      Serial.println();
      Serial.println("New Bump on its way!");
    }
    stillSum = 0;
    if (!CheckTargetProximity(sensorDeg, startPos))
    {
      errDeg = startPos - sensorDeg;
      int signOfError = errDeg / abs(errDeg);
      if (!closeToTarget)
      {
        int newRef = 5 * signOfError + sensorDeg;
        BumpTo(newRef);
      }
      else
      {
        BumpTo(startPos);
      }
    }
    else
    {
      timerMilli.set(1500);
      ChangeStepNumber(35);
    }
  }







  //STEP 35: Hold at starting position
  if (isStepNumber == 35)
  {
    OpenLoopStaticOutput(startPos);
    timerMilli.call();
    if (timerMilli.isDone)
    {
      ChangeStepNumber(40);
    }
  }



  //STEP 40: Start Throw and Send Sensor Data back to C#
  if (isStepNumber == 40)
  {
    serialM.sendMessage("START");
    ctr = 0;
    iSens = 0;
    clockTime = 1000000 / freqHz;
    timeStart = micros();
    lastTime = timeStart;
    timerSensor.set(clockTime / 50);
    timerMicro.set(clockTime);
    while (ctr < arraySize)
    {
      if (timerSensor.runMetronome())
      {
        sensorDeg = CalcSensorDeg(false);
        iSens++;
      }
      if (timerMicro.runMetronome())
      {
        analogWrite(outPin, ipDataArray[ctr]);
        digitalWrite(valvePin, switchSolenoidDataArray[ctr]);
        serialM.writeByte(byte(sensorDeg));
        //serialM.writeByte(byte(ctr));
        //serialM.writeByte(byte(psiDataInArray[ctr]));
        ctr++;

        //      timeNow = micros();

        //      thisScanTime = timeNow - lastTime;
        //      lastTime = timeNow;
        //      if (thisScanTime > maxScanTime)
        //      {
        //        maxScanTime = thisScanTime;
        //        t = ctr;
        //      }
      }
    }

    timeStop = micros();
    totalScanTime = timeStop - timeStart;
    serialM.sendMessage("END");
    ChangeStepNumber(50);

  }




  //STEP 50: Shut off Ip Transducer and switch solenoid, wait for "RECEIVED" message from C#
  if (isStepNumber == 50)
  {
    analogWrite(outPin, 0);
    digitalWrite(valvePin, false);
    if(serialM.listenAndCheck("RECEIVED"))
    {
      ChangeStepNumber(90);
    }
  }

  //STEP 90: Send Diagnostic Data
  if (isStepNumber == 90)
  {
    avgScanTime = float(totalScanTime / arraySize) / 1000.0;
    //serialM.sendMessage('T' + String(maxScanTime) + "time" + String(t));
    String stringAvg = "Avg Scan Time (ms): " + String(avgScanTime);
    String stringSensCtr = "Sensor Scan Count: " + String(iSens);
    serialM.sendMessage(stringAvg + " , " + stringSensCtr);
    totalScanTime = 0;
    ChangeStepNumber(0);
  }

  //STEP 200: Calibrate
  if (isStepNumber == 200)
  {
    //Serial.println("yo");
    if (Serial.available())
    {
      String readString = "";
      while (Serial.available())
      {
        char Char = Serial.read();
        if (Char != '\n' & Char != '\r')
        {
          readString += Char;
        }
        delay(1);
      }
      outVal = readString.toInt();
      analogWrite(outPin, outVal);
      Serial.println("New Outval of: " + readString);
    }
  }

}// end loop()










void populateDataArray(byte dataArray[])
{
  dataArray[0] = -13;
  dataArray[13] = -1;
  dataArray[arraySize - 1] = 130;
}









float CalcSensorDeg(bool isDoReading)
{
  const int LENGTH_OF_ARRAY = 50;
  static int thetaDotVals[LENGTH_OF_ARRAY] = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
  //static int thetaDotTimes[LENGTH_OF_ARRAY] = {1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
  //static long lastRecordTime = 0;
  static long sumDeg = 0;
  static float sDev = 0;
  static float output = 0;

  //sumDeg = 0;
  //sDev = 0;
  int sensordeg = 10.0 * (analogRead(sensorPin) - zeroOffset) * 360.0 / 1023.0;
  for (int i = 0; i < LENGTH_OF_ARRAY; i++)
  {

    if (i == LENGTH_OF_ARRAY - 1)
    {
      thetaDotVals[i] = sensordeg;
      sumDeg += thetaDotVals[i];
      //thetaDotTimes[i] = micros();
    }
    else if (i == 0)
    {
      sumDeg -= thetaDotVals[i];
      thetaDotVals[i] = thetaDotVals[i + 1];
    }
    else
    {
      thetaDotVals[i] = thetaDotVals[i + 1];
      //thetaDotTimes[i] = thetaDotTimes[i + 1];
    }
    //    if (i != 0)
    //    {
    //      thetaDot = (thetaDotVals[i] - thetaDotVals[i - 1]) / (thetaDotTimes[i] - thetaDotTimes[i - 1]) / float(LENGTH_OF_ARRAY - 1);
    //    }
    //sumDeg += thetaDotVals[i];
    //sDev += abs(output - thetaDotVals[i]);
  }
  output = sumDeg / float(LENGTH_OF_ARRAY) / 10.0;
  //sDev = sDev/float(LENGTH_OF_ARRAY);


  //outputting readings
  isStill = false;
  if (isDoReading)
  {
    if (timerMicro.runMetronome())
    {
      sensorDeg = CalcSensorDeg(true);
      sensorThetaDot = float(sensorDeg - lastReading) * float(freqHz);
      lastReading = sensorDeg;
      float degTol = 1.5;

      if (degTol * freqHz > sensorThetaDot & sensorThetaDot > -degTol * freqHz)
      {
        stillSum++;
        if (stillSum > 40)
        {
          isStill = true;
        }
      }
      else
      {
        //Serial.println("Zeroed Sum");
        stillSum = 0;
        isStill = false;
      }
    }
  }
  return output;
}









int GetZeroReading()
{
  const int LENGTH_OF_ARRAY = 100;
  int sumDeg = 0;
  int output = 0;
  for (int i = 0; i < LENGTH_OF_ARRAY; i++)
  {
    sumDeg += analogRead(sensorPin);
    delay(5);
  }
  output = float(sumDeg) / float(LENGTH_OF_ARRAY);
  return output;
}













bool CheckTargetProximity(int currentpos, int targetpos)
{
  float closeTol = 5.0;
  float onTargTol = .9;
  float err = targetpos - currentpos;
  onTarget = false;
  closeToTarget = false;
  if (err <= closeTol & err >= -closeTol)
  {
    closeToTarget = true;
    if (err <= onTargTol & err >= -onTargTol)
    {
      onTarget = true;
      //Serial.println("On Target!!!!!!!!!!");
    }
    else
    {
      //Serial.println("Close to Target!!!");
    }
  }
  return onTarget;
}





float OpenLoopStaticOutput(float ref_deg)
{
  double Kol = 165.0;
  double output = outMin + (Kol - outMin) * sin(ref_deg * 3.1416 / 180.0) - 5;
  if (output < outMin)
  {
    return outMin;
  }
  else
  {
    return output;
  }
}





void BumpTo(int ref)
{
  bool isBumping = false, isHolding = false, isDone = false, diag = true;
  Timer timerBUMP;

  while (!isDone)
  {

    if (!isBumping)
    {

      isBumping = true;
      isHolding = false;
      //      unsigned long bumpTime, bumpMin = 15;
      //      int A, Amin = 20;
      //      float Kt = 5;
      //      float Ka = 5 * 180 / 3.1416;

      unsigned long bumpTime, bumpMin = 70;
      int A, Amin = 20;
      float Kt = 10;
      float Ka = 10;

      int err = ref - sensorDeg;
      int signOfError = err / abs(err);

      bumpTime = bumpMin + Kt * abs(err);
      A = Amin + Ka * sin(ref * 3.1416 / 180.0);
      if (A < Amin) {
        A = Amin; // Amin is the minimum ammount of bump
      }
      if (bumpTime < bumpMin) {
        bumpTime = bumpMin;
      }


      if (signOfError < 0)
      {
        bumpTime = 1.35 * bumpTime;
        A = 1.1 * A;
      }
      //outVal = A * signOfError + outVal;
      outVal = A * signOfError + OpenLoopStaticOutput(sensorDeg);

      if (outVal > outMax)
      {
        outVal = outMax;
      }
      if (outVal < outMin)
      {
        outVal = outMin;
      }

      if (diag)
      {
        Serial.println("Bumping Amplitude: " + String(signOfError * A));
        Serial.println("Bump Time is: " + String(bumpTime));
        Serial.println("Bumping outval: " + String(outVal));
        Serial.println("Bump Error Degrees is: " + String(err));
      }
      timerBUMP.set(bumpTime);
      analogWrite(outPin, outVal);
    }

    if (isBumping & !isHolding )
    {
      timerBUMP.call();
      CalcSensorDeg(true);
      if (timerBUMP.isDone)
      {
        isHolding = true;
      }
    }

    if (isHolding)
    {
      outVal = OpenLoopStaticOutput(ref);
      analogWrite(outPin, outVal);
      //Serial.println("Position Held is: " + String(sensorDeg));
      isDone = true;
    }
  }
}


void ChangeStepNumber(int newStepNumber)
{
  isStepNumber = newStepNumber;
  if (disp)
  {
    Serial.println("New Step Number is: " + String(isStepNumber));
  }
}


void convertToIPTransducer()
{
  // if a byte = 255, the conversion to an int = -1
  for(int i=0;i<arraySize;i++)
  {
    if(psiDataInArray[i]>127)
    {
      int temp = psiDataInArray[i] - 255;
      temp = - temp;
      psiDataInArray[i] = byte(temp);
      ipDataArray[i] = ConvertPsiToIp(psiDataInArray[i]);
      switchSolenoidDataArray[i] = true;
    }
    else
    {
      ipDataArray[i] = ConvertPsiToIp(psiDataInArray[i]);
      switchSolenoidDataArray[i] = false;
    }
    
  }
}


byte ConvertPsiToIp(byte psi)
{
  byte analogOut;
  if(psi<7.5){
    analogOut = 2*psi + 65;
  }
  else{
    analogOut = byte(3.7529*float(psi) + 52.076);
  }
  return analogOut;
}
//Calibration Data
//AnalogVal     Output psi
//50            n/a
//75            5
//80            7.5
//90            10.5
//100           13.1
//110           15.9
//125           19.7
//150           26.1
//200           39
