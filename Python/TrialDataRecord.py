import json
import ThrowData


class TrialDataStruct:
    def __init__(self):
        self.TrialNumber = 0
        self.u = []
        self.y = []
        self.e = []
        self.norme = 0
        self.a = []
        self.r = []
        self.time = []
        self.json = {}
        self.json["TrialNumber"] = 0
        self.json["time"] = []
        self.json["r"] = []
        self.json["u"] = []
        self.json["y"] = []
        self.json["e"] = []
        self.json["norme"] = 0
        self.json["a"] = []

    def updatedatafromjson(self, jsondata):
        self.json = jsondata
        self.u = self.json["u"]
        self.y = self.json["y"]
        self.json["e"] = self.e
        self.norme = self.json["norme"]
        self.a = self.json["a"]
        self.r = self.json["r"]
        self.time = self.json["time"]
        self.TrialNumber = self.json["TrialNumber"]

    def refreshjsonfromdata(self):
        self.json["TrialNumber"] = self.TrialNumber
        self.json["time"] = self.time
        self.json["r"] = self.r
        self.json["u"] = self.u
        self.json["y"] = self.y
        self.json["e"] = self.e
        self.json["norme"] = self.norme
        self.json["a"] = self.a
        return self.json


class TrialDataRecord:
    def __init__(self):
        self.TrialCount = 0
        self.TrialArray = []
        self.json = {}
        self.json["TrialCount"] = 0

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
        print("Trial Data Record was saved")

    def updatedatafromjson(self, jsondata):
        self.TrialCount = jsondata["TrialCount"]
        self.TrialArray = json.loads(jsondata["TrialArray"])
        self.json = jsondata

    def refreshjsonfromdata(self):  # when adding elements, the string is added to beginning, not the end
        self.json["TrialCount"] = self.TrialCount
        self.json["TrialArray"] = json.dumps(self.TrialArray)
