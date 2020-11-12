# This file is part of gILC.
#
# gILC - Generic Iterative Learning Control for Nonlinear Systems
# Copyright (C) 2012 Marnix Volckaert, KU Leuven
#
# gILC is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# gILC is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with gILC. If not, see <http://www.gnu.org/licenses/>.
#

from numpy import *
import matplotlib.pyplot as plt
from invpen import *
import examples.CreateReferenceFile as crf

import sys

from gilc.gilc import gilcClass

sys.path.append("../gilc/")
from gilc import *

# Define simulation
#------------------
fs = 500.0                              # Sample time
pen = invpen(fs)                        # Create the inverted pendulum
ilc = gilcClass()                       # Create the gILC object
#---------------------

print(f"hello {DM.zeros(2,1)}")

def loadrefsignal(freq, _ilc):
    global fs, time, refY, initStates, N
    fs = freq
    time, refY = crf.ref(freq)  # generates reference trajectory
    N = len(time)  # Total number of samples - length of reference signal
    initStates = [refY[0] * np.pi / 180.0, 0]  # these are the initial conditions for each state
    _ilc.r = zeros((1, N))                    # Initialise the reference vector in the gILC object
    for k in range(N):
        _ilc.r[0, k] = float(refY[k]/180.0*np.pi)
    # _ilc.xinit = initStates
    return N, initStates








# DEFINE MODEL FOR ILC
#===================================================================================================
# Define problem dimensions
#--------------------------
ilc.nu = 1                              # Number of inputs
ilc.na = 1                              # Number of correction terms
ilc.nx = 2                              # Number of states
ilc.nxa = 0                             # Number of additional states
ilc.ny = 1                              # Number of outputs
#--------------------------


N, initStates = loadrefsignal(fs, ilc)




# Define Model Dynamics
#----------------------
def f(xk,uk,ak):
    I = 0.005572  # mass moment of inertia about pivot, kg-m^2
    m = 0.42987  # mass, kg
    L = 0.10057  # distance from proximal pivot to center of mass, m
    c = 0.003  # dampening term
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

#---------------
def h(xk,uk,ak):
#---------------
    y = xk[0]
    return y
#---------------
ilc.f = f                               # Provide state equations f
ilc.h = h                               # Provide output equations h
#===================================================================================================

# Define the initial state
#----------------------------------------------
ilc.xinit = initStates
print(f"ILC.XINIT: {ilc.xinit}")
#----------------------------------------------

# Add constraint on input signal
#====================================================
def hzc(xk,uk,ak):                      # Define a constrained output function
    zc = [uk[0]]
    return zc
ilc.con.hzc = hzc                       # The constraint is applied in the control step
ilc.con.zmin = -70*ones((1,N))           # Lower bound
ilc.con.zmax = 70*ones((1,N))            # Upper bound
#====================================================

# Apply a penalty to the control signal
#====================================================
def hzm(xk,uk,ak):                      # Define a minimized output function
    zm = [uk[0]]
    return zm
ilc.con.hzm = hzm                       # Penalty term is applied in the control step
ilc.con.rz = zeros((1,N))               # Reference is zero, signal itself is minimized
ilc.con.Qz = 1e2*ones((1,N))            # Provide diagonal weight for quadratic penalty term
#====================================================

# SIMULATION OF THE ILC ALGORITHM
#================================
trials = 5                              # Number of trials in the simulation
uSave = zeros((trials,N))               # Initialize saved inputs
ySave = zeros((trials,N))               # Initialize saved outputs
eSave = zeros((trials,N))               # Initialize saved tracking errors
normeSave= zeros(trials)                # Initialize saved 2-norm of tracking error
aSave = zeros((trials,N))               # Initialize saved correction signals
#------
a_i = zeros((ilc.na,N))                 # Initialize the correction signal for the first trial
u_i,xu_i = ilc.control(a_i)             # Solve the control step for the first trial
#------
for i in range(trials):
    x_i,y_i = pen.sim(initStates,u_i.astype(int))     # Simulate the system
    y_i = y_i*180.0/pi
    y_i = y_i.astype(int)*pi/180.0
    #------
    uSave[i,:] = u_i                    # Save the applied input signal
    ySave[i,:] = y_i                    # Save the measured output signal
    eSave[i,:] = ilc.r - y_i            # Save the tracking error
    normeSave[i] = linalg.norm(ilc.r - y_i) # Save the 2-norm of the tracking error
    aSave[i,:] = a_i                    # Save the correction signal
    #------
    a_i,xa_i = ilc.estimation(u_i,y_i)  # Solve the estimation problem
    u_i,xu_i = ilc.control(a_i)         # Solve the control problem
#================================

# PLOTTING
#=========
plt.figure(1)
plt.clf()
plt.subplot(3,2,1)
plt.plot(time,uSave[0,:].T,'k--')
plt.plot(time,uSave[trials-1,:].T,'k')
plt.grid()
plt.ylabel('Input [Nm]')
plt.xlabel('Time [s]')
plt.legend(('Trial 1','Trial 5'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.subplot(3,2,3)
plt.plot(time,aSave[1,:].T,'k--')
plt.plot(time,aSave[trials-1,:].T,'k')
plt.grid()
plt.ylabel('Correction signal [Nm]')
plt.xlabel('Time [s]')
plt.legend(('Trial 2','Trial 5'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.subplot(3,2,5)
plt.plot(range(1,trials+1),normeSave,'ko-')
plt.grid()
plt.xlim([1,trials])
plt.xticks(range(1,trials+1))
plt.xlabel('Iteration number [-]')
plt.ylabel('2-norm of tracking error [rad]')
#------
plt.subplot(5,2,2)
plt.plot(time,(180/pi)*ilc.r.T,'k--',label='_nolegend_')
plt.plot(time,(180/pi)*ySave[0,:].T,'k')
plt.grid()
plt.ylabel('Output [deg]')
plt.legend(('Reference','Trial 1'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.subplot(5,2,4)
plt.plot(time,(180/pi)*ilc.r.T,'k--')
plt.plot(time,(180/pi)*ySave[1,:].T,'k')
plt.grid()
plt.ylabel('Output [deg]')
plt.legend(('Reference','Trial 2'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.subplot(5,2,6)
plt.plot(time,(180/pi)*ilc.r.T,'k--')
plt.plot(time,(180/pi)*ySave[2,:].T,'k')
plt.grid()
plt.ylabel('Output [deg]')
plt.legend(('Reference','Trial 3'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.subplot(5,2,8)
plt.plot(time,(180/pi)*ilc.r.T,'k--')
plt.plot(time,(180/pi)*ySave[3,:].T,'k')
plt.grid()
plt.ylabel('Output [deg]')
plt.legend(('Reference','Trial 4'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.subplot(5,2,10)
plt.plot(time,(180/pi)*ilc.r.T,'k--')
plt.plot(time,(180/pi)*ySave[4,:].T,'k')
plt.grid()
plt.xlabel('Time [s]')
plt.ylabel('Output [deg]')
plt.legend(('Reference','Trial 5'),shadow=True, fancybox=True,loc='upper left',prop={'size':10})
#------
plt.show()
#=========
