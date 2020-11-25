import json
import datetime


class JointStruct:
    def __init__(self):
        self.Cmd = []       # u(k, i)
        self.Est = []       # a(k, i)
        self.Ref = []       # r(i)
        self.Sensor = []    # y(k, i)
        self.Time = []      # t(i)
        self.json = {}

    def updatedatafromjson(self, jsondata):
        self.Cmd = jsondata["Cmd"]
        self.Est = jsondata["Est"]
        self.Ref = jsondata["Ref"]
        self.Sensor = jsondata["Sensor"]
        self.Time = jsondata["Time"]
        self.json = jsondata

    def refreshjsonfromdata(self):
        self.json["Cmd"] = self.Cmd
        self.json["Est"] = self.Est
        self.json["Ref"] = self.Ref
        self.json["Sensor"] = self.Sensor
        self.json["Time"] = self.Time
        return self.json


class ThrowData:
    def __init__(self):
        self.TrialNumber = -1
        self.SamplingFrequency = 0
        self.ArraySize = 0
        self.DateCalculated = {}
        self.DateExecuted = {}
        self.Shoulder = JointStruct()
        self.Elbow = JointStruct()
        self.json = {}
        self.json["TrialNumber"] = 0
        self.json["Shoulder"] = {}
        self.json["Elbow"] = {}

    def updatedatafromjson(self, jsondata):
        self.TrialNumber = jsondata["TrialNumber"]
        self.SamplingFrequency = jsondata["SamplingFrequency"]
        self.ArraySize = jsondata["ArraySize"]
        self.DateCalculated = datetime.datetime.strptime(jsondata["DateCalculated"], '%Y-%m-%dT%H:%M:%S.%fZ')
        self.DateExecuted = datetime.datetime.strptime(jsondata["DateExecuted"], '%Y-%m-%dT%H:%M:%S.%fZ')
        self.Shoulder.updatedatafromjson(jsondata["Shoulder"])
        self.Elbow.updatedatafromjson(jsondata["Elbow"])
        self.json = jsondata

    def readfile(self, filename):
        jsonfile = open(filename, "r")
        jsondata = json.load(jsonfile)
        self.updatedatafromjson(jsondata)
        jsonfile.close()

    def writefile(self, filename):
        jsonfile = open(filename, "w")
        self.refreshjsonfromdata()
        json.dump(self.json, jsonfile)
        jsonfile.close()

    def refreshjsonfromdata(self):   # when adding elements, the string is added to beginning, not the end
        self.json["Elbow"] = self.Elbow.refreshjsonfromdata()
        self.json["Shoulder"] = self.Shoulder.refreshjsonfromdata()
        self.json["TrialNumber"] = self.TrialNumber
        self.json["SamplingFrequency"] = self.SamplingFrequency
        self.json["ArraySize"] = self.ArraySize
        self.json["DateCalculated"] = self.DateCalculated.strftime('%Y-%m-%dT%H:%M:%S.%fZ')
        self.json["DateExecuted"] = self.DateExecuted.strftime('%Y-%m-%dT%H:%M:%S.%fZ')


