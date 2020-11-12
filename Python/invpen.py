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

from gilc.plant import *
import random
from casadi import *

class invpen(plant):

    fs = 100   # sampling frequency

    def __init__(self, _fs):
        self.nx = 2
        self.ny = 1
        self.noisestd = 0.0
        global fs
        fs = _fs

    # Define System Dynamics
    #=======================
    def f(self, xk, uk):
        I = 0.005572  # mass moment of inertia about pivot, kg-m^2
        m = .5  # 0.42987  # mass, kg
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
                       xk[1] + (Ts / I) * (-c * xk[1] - 9.81 * m * L * sin(xk[0]) + r * cNoP * A * (uk[0])))
        return xkp1
    #---------------
    def h(self,xk,uk):
        #---------------
        y = xk[0]
        y += random.gauss(0.0,self.noisestd)
        return y
    #=======================
