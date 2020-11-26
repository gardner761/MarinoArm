import matplotlib.pyplot as plt
from invpen import *
from gilc.gilc import *
import CreateReferenceFile as crf
from TrialDataRecord import*
import config


fileTrialData = config.TRIALDATARECORD_FILEPATH  # json file path to be read by CSharp running this script
# fs = config.SAMPLING_FREQUENCY      # Sampling Frequency, Hz
time = []
ref_shoulder = []
ref_elbow = []
initStates = []
PSI_LIMIT = 40


# Define Model Dynamics
#----------------------
def f(xk,uk,ak):
    I = 0.005572  # mass moment of inertia about pivot, kg-m^2
    m = .5 #0.42987  # mass, kg
    L = 0.10057  # distance from proximal pivot to center of mass, m
    c = 0.01  # dampening term
    r = 11.75 / 1000  # radius at which the tension acts, m
    dbore = 16  # diameter of the air cylinder bore, mm
    drod = 5  # diameter of the air cylinder rod, mm
    A = ((dbore / 25.4 / 2) ** 2 - (drod / 25.4 / 2) ** 2) * np.pi  # area of the cylinder, in^2
    cNoP = 4.45  # newtons/lbs
    # Torque = radius * Force, Force = Pressure*A
    global fs
    Ts = 1 / fs  # time step, sec

    xkp1 = vertcat(xk[0] + Ts * xk[1],
               xk[1] + (Ts / I) * (-c * xk[1] - 9.81 * m * L * sin(xk[0]) + r * cNoP * A * (uk[0] +ak[0])))
    return xkp1


def h(xk,uk,ak):
    y = xk[0]
    return y



# Add constraint on input signal
#====================================================
def hzc(xk,uk,ak):                      # Define a constrained output function
    zc = [uk[0]]
    return zc


# Apply a penalty to the control signal
#====================================================
def hzm(xk,uk,ak):                      # Define a minimized output function
    zm = [uk[0]]
    return zm




def convertlisttonparray(lst: list):
    n = len(lst)
    output = zeros([1, n])
    for i in range(n):
        output[0, i] = lst[i]
    return output


def assignref(_ilc):
    global arraysize
    N = arraysize
    _ilc.r = zeros((1, N))                    # Initialise the reference vector in the gILC object
    for k in range(N):
        _ilc.r[0, k] = float(ref_shoulder[k]/180.0*np.pi)
        # _ilc.r[1, k] = float(ref_elbow[k]/180.0*np.pi)
        # TODO - Uncomment the line above

    print(f"Last Ref Val is: {_ilc.r[0,N-1]}")


def loadrefsignal(_ilc):
    global time, ref_shoulder, ref_elbow, initStates
    ref_shoulder, ref_elbow = crf.ref(time)  # generates reference trajectory
    print(f"ref_shoulder: {ref_shoulder}")

    if len(time) != arraysize:
        print(f"Calculated time series length: {len(time)} does not match ArraySize value: {arraysize}")
    initStates = [ref_shoulder[0] * np.pi / 180.0, 0]  # these are the initial conditions for each state
    # TODO - fix this to include elbow, this array stays one dimensional, just add elbow states onto the end
    assignref(_ilc)


def calcthrow(throwdata: ThrowData):
    # Define simulation
    # ------------------
    global fs, arraysize, initStates, ref_shoulder, ref_elbow, time
    time = throwdata.Shoulder.Time
    fs = throwdata.SamplingFrequency  # Sample frequency, hz
    arraysize = throwdata.ArraySize
    print(f"throwdata.ArraySize: {throwdata.ArraySize}")
    N = arraysize
    pen = invpen(fs)  # Create the inverted pendulum
    ilc = gilcClass()  # Create the gILC object
    # ---------------------

    # DEFINE MODEL FOR ILC
    # ===================================================================================================
    # Define problem dimensions
    # --------------------------
    ilc.nu = 1  # Number of inputs
    ilc.na = 1  # Number of correction terms
    ilc.nx = 2  # Number of states
    ilc.nxa = 0  # Number of additional states
    ilc.ny = 1  # Number of outputs
    # --------------------------

    loadrefsignal(ilc)

    ilc.f = f  # Provide state equations f
    ilc.h = h  # Provide output equations h
    # ===================================================================================================

    # Define the initial state
    # ----------------------------------------------
    ilc.xinit = initStates
    # ----------------------------------------------

    ilc.con.hzm = hzm  # Penalty term is applied in the control step
    ilc.con.rz = zeros((1, N))  # Reference is zero, signal itself is minimized
    ilc.con.Qz = 1e4 * ones((1, N))  # Provide diagonal weight for quadratic penalty term
    # ====================================================

    ilc.con.hzc = hzc  # The constraint is applied in the control step
    ilc.con.zmax = PSI_LIMIT * ones((1, N))  # Upper bound
    ilc.con.zmin = -PSI_LIMIT * ones((1, N))  # Lower bound

    #ilc.est.Qy = 1e4 * ones((ilc.ny, N))
    #ilc.con.Qy = 1e4 * ones((ilc.ny, N))

    if throwdata.TrialNumber == 0:
        a = zeros((ilc.na, N))  # Initialize the correction signal for the first trial
        print("Initialized the correction signal, a(k), for the first trial")
    elif throwdata.TrialNumber > 0:
        if isinstance(throwdata.Shoulder.Sensor, list):
            # TODO - add elbow data here
            u_last = convertlisttonparray(throwdata.Shoulder.Cmd)
            y_last = convertlisttonparray(throwdata.Shoulder.Sensor) * pi/180
        a, xa = ilc.estimation(u_last, y_last)  # Solve the estimation problem (calculates the correction signal)

    r = ref_shoulder  # TODO - combine ref_elbow into this, probably as a 2D matrix
    u, xu = ilc.control(a)  # Solve the control problem
    return u, a, ilc, time, r

# TODO - update this method for elbow
# region Simulate Throw Method


def simthrow(u_i, _ilc):
    global fs
    if isinstance(u_i, list):
        u_i = convertlisttonparray(u_i)
    pen = invpen(fs)                          # Create the inverted pendulum
    x_i, y_i = pen.sim(initStates, u_i)       # Simulate the system
    err = _ilc.r - y_i                        # Save the tracking error
    norme = linalg.norm(_ilc.r - y_i)         # Save the 2-norm of the tracking error
    return y_i, err, norme

# endregion

# TODO - update this method for elbow
# region Plotting Simulation Method


def plottrials(trials, uSave, aSave, ySave, normeSave, rSave, il):
    plt.figure(1)
    plt.clf()
    plt.subplot(3,2,1)
    plt.plot(time,uSave[0,:].T,'k--')
    plt.plot(time,uSave[trials-1,:].T,'k')
    plt.grid()
    plt.ylabel('Input [Nm]')
    plt.xlabel('Time [s]')
    plt.legend(('Trial 1','Trial 5'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
    # ------
    plt.subplot(3,2,3)
    plt.plot(time,aSave[1,:].T,'k--')
    plt.plot(time,aSave[trials-1,:].T,'k')
    plt.grid()
    plt.ylabel('Correction signal [Nm]')
    plt.xlabel('Time [s]')
    plt.legend(('Trial 2','Trial 5'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
    # ------
    plt.subplot(3,2,5)
    plt.plot(range(1,trials+1),normeSave,'ko-')
    plt.grid()
    plt.xlim([1,trials])
    plt.xticks(range(1,trials+1))
    plt.xlabel('Iteration number [-]')
    plt.ylabel('2-norm of tracking error [rad]')

    for i in range(trials):
        plt.subplot(5,2,2 + 2*i)
        plt.plot(time,(180/pi)*rSave,'k--',label='_nolegend_')
        plt.plot(time,(180/pi)*ySave[i,:].T,'k')
        plt.grid()
        plt.ylabel('Output [deg]')
        plt.legend(('Reference','Trial 1'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
    plt.show()

# endregion
