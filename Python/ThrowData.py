import json


class JointStruct:
    def __init__(self):
        self.Ref = []       # r(i)
        self.Cmd = []       # u(k, i)
        self.Est = []       # a(k, i)
        self.Sensor = []    # y(k, i)
        self.json = {}

    def updatedatafromjson(self, jsondata):
        self.Cmd = jsondata["Cmd"]
        self.Est = jsondata["Est"]
        self.Ref = jsondata["Ref"]
        self.Sensor = jsondata["Sensor"]
        self.json = jsondata

    def refreshjsonfromdata(self):
        self.json["Cmd"] = self.Cmd
        self.json["Est"] = self.Est
        self.json["Ref"] = self.Ref
        self.json["Sensor"] = self.Sensor
        return self.json


class ThrowData:
    def __init__(self):
        self.TrialNumber = -1
        self.SamplingFrequency = 0
        self.Shoulder = JointStruct()
        self.Elbow = JointStruct()
        self.json = {}
        self.json["TrialNumber"] = 0
        self.json["Shoulder"] = {}
        self.json["Elbow"] = {}

    def updatedatafromjson(self, jsondata):
        self.TrialNumber = jsondata["TrialNumber"]
        self.SamplingFrequency = jsondata["SamplingFrequency"]
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


