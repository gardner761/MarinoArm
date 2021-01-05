////////////////////////////////////////////////////////////////////////////
//MARINOARM (SLAVE)//
//This board is a slave controller for the ip transducers and potentiometers
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
int shSensorPin = A0; // select the input pin for the potentiometer
int elSensorPin = A1;
int shValvePin = 22; // select the pin for the LED
int elValvePin = 23;
int shOutPin = 11;
int elOutPin = 12;
int ledPin = 13;
int shSensorDeg;
int elSensorDeg;
int shZeroOffset;
int elZeroOffset;

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
int SamplingFrequency = 100; //this should match the C# sampling frequency
const int sizer = 101;   //326; //this should be larger than what the arraySize value is
byte shCmdData[sizer]; //WARNING!!!!! CHANGE THIS BACK TO match the RAP arraySize
byte elCmdData[sizer];
bool shSolenoidData[sizer];
bool elSolenoidData[sizer];
bool switchOn;



void setup()
{
  serialM.begin(115200);
  pinMode(shValvePin, OUTPUT);
  pinMode(shOutPin, OUTPUT);
  pinMode(elValvePin, OUTPUT);
  pinMode(elOutPin, OUTPUT);
  pinMode(ledPin, OUTPUT);
  analogWrite(shOutPin, 0);
  digitalWrite(shValvePin, LOW);
  analogWrite(elOutPin, 0);
  digitalWrite(elValvePin, LOW);

  // INITIALIZING THE ROBOT SENSORS AND TRAVELLING TO START POSITION
  shZeroOffset = GetZeroReading(shSensorPin);
  elZeroOffset = GetZeroReading(elSensorPin);
  //Serial.println(zeroOffset);

  //CONNECTING: waiting for hello and then Sending Hello Back
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
    clockTime = 1000000 / SamplingFrequency; //timeStep in microseconds
    timerSensor.set(clockTime / 50); //goes off every 200us, helps with sensor value averaging
    timerMicro.set(clockTime); //goes off every 10ms or 10,000us
    timerDISP.set(clockTime * 50); //goes off every 500ms 
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
      Serial.println("SensorDeg: " + String(shSensorDeg));
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
        timerMilli.set(1500); // goes off every 1500ms
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

    if (serialM.collectNewData(shCmdData, arraySize))
    {
      ChangeStepNumber(25);
    }
    else //blinks rapidly until all data has been received
    {
      timerMilli.call();
      if (timerMilli.isDone)
      {
        timerMilli.set(150);// goes off every 150ms
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
    convertToIPTransducer(shCmdData, shSolenoidData); //populates the ipDataArray and shSolenoidData
    convertToIPTransducer(elCmdData, elSolenoidData);
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
    if (!CheckTargetProximity(shSensorDeg, startPos))
    {
      errDeg = startPos - shSensorDeg;
      int signOfError = errDeg / abs(errDeg);
      if (!closeToTarget)
      {
        int newRef = 5 * signOfError + shSensorDeg;
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
    clockTime = 1000000 / SamplingFrequency;
    timeStart = micros();
    lastTime = timeStart;
    timerSensor.set(clockTime / 50);
    timerMicro.set(clockTime);
    float* sd;

    while (ctr < arraySize)
    {
      if (timerSensor.runMetronome())
      {
        sd = CalcSensorDeg(false);
        iSens++;
      }
      // Writes and Reads at the sampling frequency
      if (timerMicro.runMetronome())
      {
        analogWrite(shOutPin, shCmdData[ctr]);
        digitalWrite(shValvePin, shSolenoidData[ctr]);
        analogWrite(elOutPin, elCmdData[ctr]);
        digitalWrite(elValvePin, elSolenoidData[ctr]);

        //The order is critical, write shoulder first then elbow
        serialM.writeByte(byte(sd[0]));
        serialM.writeByte(byte(sd[1]));

        ctr++;
      }
    }

    timeStop = micros();
    totalScanTime = timeStop - timeStart;
    serialM.sendMessage("END");
    shSensorDeg = sd[0];
    elSensorDeg = sd[1];
    ChangeStepNumber(50);
  }




  //STEP 50: Shut off Ip Transducer and switch solenoid, wait for "RECEIVED" message from C#
  if (isStepNumber == 50)
  {
    analogWrite(shOutPin, 0);
    digitalWrite(shValvePin, false);
    analogWrite(elOutPin, 0);
    digitalWrite(elValvePin, false);
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
      analogWrite(shOutPin, outVal);
      Serial.println("New Outval of: " + readString);
    }
  }

}// end loop()

/// <summary>
/// used for testing, populates the array passed in with a few values
/// </summary>
/// <param name="dataArray"></param>
void populateDataArray(byte dataArray[])
{
  dataArray[0] = -13;
  dataArray[13] = -1;
  dataArray[arraySize - 1] = 130;
}

/// <summary>
/// window averaging of sensor deg values
/// </summary>
/// <param name="isCheckIfStill">
/// writes current average values to global sensorDeg values
/// </param>
/// <returns>
/// outputs the current shoulder deg average value
/// </returns>
float* CalcSensorDeg(bool isCheckIfStill)
{
  const int LENGTH_OF_ARRAY = 50;
  static int shoulderVals[LENGTH_OF_ARRAY] = {}; //FIFO array
  static int elbowVals[LENGTH_OF_ARRAY] = {}; //FIFO array

  static long sumShDeg = 0;
  static long sumElDeg = 0;

  static float output[] = {0,0};
  static float shOutput = 0;
  static float elOutput = 0;

  int shDeg = round(10.0 * (analogRead(::shSensorPin) - ::shZeroOffset) * 360.0 / 1023.0);
  int elDeg = round(10.0 * (analogRead(::elSensorPin) - ::elZeroOffset) * 360.0 / 1023.0);

  // Rotates the all values in shoulderVals one index value to the left, 
  // first value (index 0) is kicked out and last value is updated with new reading
  for (int i = 0; i < LENGTH_OF_ARRAY; i++)
  {
    if (i == 0)
    {
    sumShDeg -= shoulderVals[i];
    sumElDeg -= elbowVals[i];
    shoulderVals[i] = shoulderVals[i + 1];
    elbowVals[i] = elbowVals[i + 1];
    }
    else if (i < LENGTH_OF_ARRAY - 1)
    {
      shoulderVals[i] = shoulderVals[i + 1];
      elbowVals[i] = elbowVals[i + 1];
    }
    else
    {
        shoulderVals[i] = shDeg;
        elbowVals[i] = elDeg;
        sumShDeg += shoulderVals[i];
        sumElDeg += elbowVals[i];
    }
  }
  shOutput = sumShDeg / float(LENGTH_OF_ARRAY) / 10.0;
  elOutput = sumElDeg / float(LENGTH_OF_ARRAY) / 10.0;
  output[0] = shOutput;
  output[1] = elOutput;


  //outputting readings
  ::isStill = false;
  if (isCheckIfStill)
  {
    if (timerMicro.runMetronome())
    {
      ::shSensorDeg = shOutput;
      ::elSensorDeg = elOutput;
      sensorThetaDot = float(shDeg - lastReading) * float(::SamplingFrequency);
      lastReading = shDeg;
      float degTol = 1.5;

      if (degTol * SamplingFrequency > sensorThetaDot & sensorThetaDot > -degTol * SamplingFrequency)
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

/// <summary>
/// Calculates the zero reading of the sensor, it averages 100 data points collected over 1/2sec timespan
/// </summary>
/// <param name="pin">
/// sensor pin value
/// </param>
/// <returns>
/// averaged reading output
/// </returns>
int GetZeroReading(int pin)
{
  const int LENGTH_OF_ARRAY = 100;
  int sumDeg = 0;
  int output = 0;
  for (int i = 0; i < LENGTH_OF_ARRAY; i++)
  {
    sumDeg += analogRead(pin);
    delay(5);
  }
  output = round(float(sumDeg) / float(LENGTH_OF_ARRAY));
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

      int err = ref - ::shSensorDeg;
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
      outVal = A * signOfError + OpenLoopStaticOutput(::shSensorDeg);

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
      analogWrite(::shOutPin, outVal);
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
      analogWrite(::shOutPin, outVal);
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

void convertToIPTransducer(byte dataArray[], bool solenoidDataArray[])
{
  // if a byte = 255, the conversion to an int = -1
  for(int i=0;i<arraySize;i++)
  {
    if(dataArray[i]>127)
    {
      int temp = dataArray[i] - 255;
      temp = - temp;
      dataArray[i] = byte(temp);
      dataArray[i] = ConvertPsiToIp(dataArray[i]);
      solenoidDataArray[i] = true;
    }
    else
    {
      dataArray[i] = ConvertPsiToIp(dataArray[i]);
      solenoidDataArray[i] = false;
    }
    
  }
}

byte ConvertPsiToIp(byte psi)
{
    byte analogOut;
    if (psi < 7.5) {
        analogOut = 2 * psi + 65;
    }
    else {
        analogOut = byte(round(3.7529 * float(psi) + 52.076));
    }
    return analogOut;

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
}
