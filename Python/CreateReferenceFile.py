import numpy as np
from scipy.interpolate import CubicSpline
import matplotlib.pyplot as plt

diag = False


def ref(fs):
    tSh = np.array([0.0, 0.1, 0.2, 0.3, 0.425,
                    0.6, .8, 1.0, 1.05, 1.15,
                    1.2, 1.25, 1.3, 1.35, 1.4,
                    1.5, 1.7, 1.85, 2.0])
    thetaSh = 180.0/np.pi*np.array([0.95, 0.951, 0.955, 0.969,  1.02,
                                    1.25, 1.75, 2.22, 2.25, 2.15,
                                    2.0, 1.85, 1.7, 1.6, 1.5,
                                    1.41, 1.365, 1.355, 1.354])

    timeStep = 1.0/fs
    xs = np.arange(tSh[0], tSh.max() + timeStep, timeStep)
    x = tSh
    y = thetaSh
    cs = CubicSpline(x, y)
    ys = cs(xs)

    if diag:
        plt.figure(figsize=(6.5, 4))
        plt.plot(x, y, 'o', label='data')
        plt.plot(xs, ys, label="Spline")
        plt.show()

    return xs, ys
