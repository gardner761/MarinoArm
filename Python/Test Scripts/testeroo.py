import os
from TrialDataRecord import*
from ThrowData import*
import matplotlib.pyplot as plt
import numpy as np

file_tdr = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\TrialDataRecord.json"
# openfile = open(file_tdr, 'w')
# openfile.close()


def writeFirstJsonFromCSharp():
    _data = ThrowData()
    _data.TrialNumber = 0;
    _data.SamplingFrequency = 100;
    file = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\DataFromCSharp.json"
    _data.writefile(file)


writeFirstJsonFromCSharp()
trials = 1
for i in range(trials):
    os.system('python MarinoArm.py')  # make sure runSim is set to True
    dataIn = ThrowData()
    fileIn = "C:\\Users\\gardn\\source\\repos\\MarinoArm\\Python\\DataFromCSharp.json"
    dataIn.readfile(fileIn)
    trialNumber = dataIn.TrialNumber
    print(f"Testeroo Trial Number!! is {i+1} and Csharp is {trialNumber}")

print("Done")

# Plotting Results
if 1 == 1:
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
