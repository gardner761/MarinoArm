import commandcalculator
from TrialDataRecord import*
from ThrowData import *
import numpy as np
import matplotlib.pyplot as plt

# Settings
runSim = True  # disable this normally, use just for simulation

# Defines
isFirstThrow = False


# 1. Read the json data from C Sharp
print("-----------------------------------------------------------------------------------")
print("Step 1 - Read the Json Data from C#:")
dataIn = ThrowData()
fileIn = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\DataFromCSharp.json"
dataIn.readfile(fileIn)

if dataIn.TrialNumber == 0:
    isFirstThrow = True
    print("   First Throw Detected")


print(f"   TrialNumber: {dataIn.TrialNumber}")
print(f"   Sampling Frequency: {dataIn.SamplingFrequency} Hz")
print('\n', '\n')


# 2. Calculate new throw data and load it to dataOut object
print("-----------------------------------------------------------------------------------")
print("Step 2 - Calculate new throw data and load it to dataOut object")
# commandcalculator.loadrefsignal(dataIn.SamplingFrequency)
u, a, ilc, time, r = commandcalculator.calcthrow(dataIn, dataIn.SamplingFrequency)
print(f"Length of Control Signal Array: {np.size(u)}")



# 3. Write the dataOut Object to the json file
print("-----------------------------------------------------------------------------------")
print("Step 3 - Write the dataOut Object to the json file")
dataOut = ThrowData()
dataOut.updatedatafromjson(dataIn.json)  # this works like a clone func, you can't just set one object equal to another
dataOut.TrialNumber = dataIn.TrialNumber + 1
print(f"Increased the trial number for Python Json by one, it is now: {dataOut.TrialNumber}")
cmdSh = u[0, :].tolist()
estSh = a[0, :].tolist()
refSh = commandcalculator.refY.tolist()
dataOut.Shoulder.Cmd = cmdSh
dataOut.Shoulder.Est = estSh
dataOut.Shoulder.Ref = refSh
fileOut = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\DataFromPython.json"
dataOut.writefile(fileOut)

# 4. Simulate the throw (disabled normally)
if runSim:
    y, e, norme = commandcalculator.simthrow(u, ilc)
    y = (180 / np.pi) * y
    e = (180 / np.pi) * e
    dataSim = ThrowData()
    dataSim.readfile(fileOut)
    dataSim.Shoulder.Sensor = y[0, :].tolist()
    # dataIn.Shoulder.Cmd = u[0, :].tolist()
    # dataIn.Shoulder.Ref = r.tolist()
    # dataIn.TrialNumber = dataIn.TrialNumber + 1
    dataSim.writefile(fileIn)


# 5. Store the data to the Trial Data Record
file_tdr = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\TrialDataRecord.json"
tdr = TrialDataRecord()

if dataOut.TrialNumber > 1:  # this loads an existing trial data record as long as it is not the first trial
    tdr.readfile(file_tdr)

tdr.TrialCount = tdr.TrialCount + 1
sd = TrialDataStruct()
sd.time = time.tolist()
sd.r = r.tolist()

if runSim:
    sd.e = e[0, :].tolist()
    sd.y = y[0, :].tolist()
    sd.norme = norme

sd.a = estSh
sd.u = cmdSh

sd.TrialNumber = dataOut.TrialNumber
tdr.json[f"trial{sd.TrialNumber}"] = sd.refreshjsonfromdata()
# tdr.TrialArray.append(sd)

#print(f"trial{sd.TrialNumber}")
#print(f"tdr.json[trial{sd.TrialNumber}][y]")
#print(tdr.json[f"trial{sd.TrialNumber}"]["y"])

tdr.writefile(file_tdr)



# 6. Plotting Results
showPlot = False
if showPlot:
    file_tdr = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\TrialDataRecord.json"
    tdr = TrialDataRecord()
    tdr.readfile(file_tdr)
    # plt.clf()
    legendlist = []
    trialnumbers = []
    trialnorme = []

    fig, ax = plt.subplots(3, 1)
    for i in range(tdr.TrialCount):
        sd = TrialDataStruct()
        trialname = f"trial{i+1}"
        trialjson = tdr.json[trialname]
        sd.updatedatafromjson(trialjson)
        trialnumbers.append(sd.TrialNumber)
        trialnorme.append(sd.norme)
        if i == 0:
            ax[1].plot(sd.time, np.array(sd.r), 'k--')
        ax[1].plot(sd.time, np.array(sd.y))
        ax[1].grid('major')
        ax[1].set_ylabel('Shoulder Angle [deg]')
        legendlist.append(f'Trial{sd.TrialNumber}')
        ax[0].plot(sd.time, np.array(sd.u))
    ax[0].legend(legendlist, shadow=True, fancybox=True, loc='upper left', prop={'size': 10})
    legendlist.insert(0, "ref")
    ax[1].legend(legendlist, shadow=True, fancybox=True, loc='upper left', prop={'size': 10})

    ax[2].set_ylabel('2-Norm of Tracking Error')
    ax[2].set_xlabel('Trial Number')
    ax[2].set_xlim([0, tdr.TrialCount + 1])
    ax[2].set_xticks(range(tdr.TrialCount + 1))
    ax[2].plot(trialnumbers, trialnorme, 'k*')

    plt.show()