from ThrowData import ThrowData
from numpy import*


dataIn = ThrowData() 
fileIn = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\DataFromCSharp.json"
dataIn.readfile(fileIn)


dataOut = ThrowData()
dataOut.Elbow.Ref = [1, 2, 3, 4]
dataOut.Elbow.Cmd = [4, 4, 4, 4]
dataOut.Shoulder.Ref = [2, 2, 2, 2]
dataOut.Shoulder.Cmd = [13, 77, 13, 77, 13, 77, 111]
dataOut.Elbow.Cmd = [13, 13, 13, 13, 13, 13, 13]

fileOut = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\DataFromPython.json"
dataOut.writefile(fileOut)

