using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Windows;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using ArmLibrary;
using PythonIntegration;
using SerialLibrary;
using static SerialLibrary.RobotArmProtocol;
using SciChart.Charting2D.Interop;
using System.Security.Cryptography;
using System.Windows.Media;

namespace WPFUI.ViewModels
{
    class ThrowingViewModel : Screen
    {
        #region Defines

        /// <summary>
        /// Determines the refresh rate of the plots
        /// </summary>
        private int refreshPlotCount = 2; //amount of trials to run until the plot is refreshed

        private static String saveIconGray = "/Resources/SaveIconGray.png";
        private static String saveIconGreen = "/Resources/SaveIconGreen.png";
        
        /// <summary>
        /// determines if line plotting will start a new line (if false) or add to the existing line (if true)
        /// </summary>
        bool isBuiltNewLine;
        public enum LineType
        {
            ShoulderRef,
            ElbowRef,
            ShoulderSensor,
            ElbowSensor,
            ShoulderCmd,
            ElbowCmd
        }

        RobotArmProtocol robotArmProtocol;

        private SolidColorBrush saveButtonBackgroundBrush = Brushes.Azure;

        #endregion

        #region Properties

        #region Save Button

        private SolidColorBrush colorIsMouseOver;
        public SolidColorBrush ColorIsMouseOver
        {
            get { return colorIsMouseOver; }
            set 
            { 
                colorIsMouseOver = value;
                NotifyOfPropertyChange(() => ColorIsMouseOver);
            }
        }

        private string saveButtonImage = saveIconGray;
        public string SaveButtonImage
        {
            get { return saveButtonImage; }
            set 
            { 
                saveButtonImage = value;
                NotifyOfPropertyChange(() => SaveButtonImage);
            }
        }
        private Visibility saveButtonVisibility;
        public Visibility SaveButtonVisibility
        {
            get
            {
                return saveButtonVisibility;
            }
            set
            {
                saveButtonVisibility = value;
                if (value == Visibility.Visible)
                {
                    SaveButtonImage = saveIconGray;
                    ColorIsMouseOver = saveButtonBackgroundBrush;
                }
                NotifyOfPropertyChange(() => SaveButtonVisibility);
            }
        }

        #endregion

        #region Radio Button Group
        private bool calculateChecked;
        public bool CalculateChecked
        {
            get { return calculateChecked; }
            set
            {
                if (value.Equals(calculateChecked)) return;
                calculateChecked = value;
                if (value)
                {
                    robotArmProtocol.ThrowTypeSelected = ThrowType.Calculated;
                }
                NotifyOfPropertyChange(() => CalculateChecked);
            }
        }

        private bool savedChecked;
        public bool SavedChecked
        {
            get { return savedChecked; }
            set
            {
                if (value.Equals(savedChecked)) return;
                savedChecked = value;
                if (value)
                {
                    robotArmProtocol.ThrowTypeSelected = ThrowType.Saved;
                }
                NotifyOfPropertyChange(() => SavedChecked);
            }
        }

        private bool rerunChecked;
        public bool RerunChecked
        {
            get { return rerunChecked; }
            set
            {
                if (value.Equals(rerunChecked)) return;
                rerunChecked = value;
                if (value)
                {
                    robotArmProtocol.ThrowTypeSelected = ThrowType.Rerun;
                }
                NotifyOfPropertyChange(() => RerunChecked);
            }
        }

        private bool rerunIsEnabled;
        public bool RerunIsEnabled
        {
            get { return rerunIsEnabled; }
            set
            {
                if (value.Equals(rerunIsEnabled)) return;
                rerunIsEnabled = value;
                NotifyOfPropertyChange(() => RerunIsEnabled);
            }
        }

        #endregion

        #region Plot Models

        private PlotModel shRefPlotModel;
        public PlotModel ShRefPlotModel
        {
            get
            {
                return shRefPlotModel;
            }
            set
            {
                shRefPlotModel = value;
                NotifyOfPropertyChange(() => ShRefPlotModel);
            }
        }

        private PlotModel shCmdPlotModel;
        public PlotModel ShCmdPlotModel
        {
            get
            {
                return shCmdPlotModel;
            }
            set
            {
                shCmdPlotModel = value;
                NotifyOfPropertyChange(() => ShCmdPlotModel);
            }
        }

        private PlotModel elRefPlotModel;
        public PlotModel ElRefPlotModel
        {
            get
            {
                return elRefPlotModel;
            }
            set
            {
                elRefPlotModel = value;
                NotifyOfPropertyChange(() => ElRefPlotModel);
            }
        }

        private PlotModel elCmdPlotModel;
        public PlotModel ElCmdPlotModel
        {
            get
            {
                return elCmdPlotModel;
            }
            set
            {
                elCmdPlotModel = value;
                NotifyOfPropertyChange(() => ElCmdPlotModel);
            }
        }

        #endregion

        #region Miscellaneous
        public int ThrowCtr
        {
            get
            {
                return robotArmProtocol.ThrowCtr;
            }
            set
            {
                robotArmProtocol.ThrowCtr = value;
            }
        }
        private Visibility startButtonVisibility;
        public Visibility StartButtonVisibility
        {
            get
            {
                return startButtonVisibility;
            }
            set
            {
                startButtonVisibility = value;
                NotifyOfPropertyChange(() => StartButtonVisibility);
            }
        }
        #endregion

        #endregion

        #region Constructors
        public ThrowingViewModel()
        {
            if (ShRefPlotModel == null)
            {
                ShRefPlotModel = new PlotModel();
                int[] minmax = { 0, GlobalVariables.ARRAY_SIZE * 1000/GlobalVariables.SAMPLING_FREQUENCY, -20, 200 };
                SetupPlotModel(ShRefPlotModel, "Shoulder Trajectory vs. Time","Angle (deg)", minmax);
            }

            if (ShCmdPlotModel == null)
            {
                ShCmdPlotModel = new PlotModel();
                int[] minmax = { 0, GlobalVariables.ARRAY_SIZE * 1000 / GlobalVariables.SAMPLING_FREQUENCY, -50, 50 };
                SetupPlotModel(ShCmdPlotModel, "Shoulder Cmd Input vs. Time", "Cmd (psi)", minmax);
            }

            if (ElRefPlotModel == null)
            {
                ElRefPlotModel = new PlotModel();
                int[] minmax = { 0, GlobalVariables.ARRAY_SIZE * 1000 / GlobalVariables.SAMPLING_FREQUENCY, -20, 200 };
                SetupPlotModel(ElRefPlotModel, "Elbow Trajectory vs. Time", "Angle (deg)", minmax);
            }

            if (ElCmdPlotModel == null)
            {
                ElCmdPlotModel = new PlotModel();
                int[] minmax = { 0, GlobalVariables.ARRAY_SIZE * 1000 / GlobalVariables.SAMPLING_FREQUENCY, -50, 50 };
                SetupPlotModel(ElCmdPlotModel, "Elbow Cmd Input vs. Time", "Cmd (psi)", minmax);
            }
            SaveButtonVisibility = Visibility.Hidden;
        }

        public ThrowingViewModel(RobotArmProtocol rap)
            : this()
        {
            robotArmProtocol = rap;
            robotArmProtocol.refreshPlotCount = refreshPlotCount;
            robotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
            robotArmProtocol.masp.updatedDataEvent += RobotArmProtocol_updatedDataEvent;
            
            CalculateChecked = true;
        }

        #endregion

        #region Event Handlers
        /// <summary>
        /// Reacts to state changes in the RAP, usually as a result of button-pushing on this UI
        /// </summary>
        /// <param name="message"></param>
        private void RobotArmProtocol_stateChangedEvent(string message)
        {
            switch (robotArmProtocol.State)
            {
                case States.OnStartup:
                    break;
                case States.Initialized:
                    break;
                case States.Idle:
                    StartButtonVisibility = Visibility.Visible;
                    break;
                case States.TaskPlanning:
                    SaveButtonVisibility = Visibility.Hidden;
                    if (ThrowCtr % refreshPlotCount == 1 & ShRefPlotModel != null & ElRefPlotModel != null)
                    {
                        ClearPlot(ShRefPlotModel);
                        ClearPlot(ShCmdPlotModel);
                        ClearPlot(ElRefPlotModel);
                        ClearPlot(ElCmdPlotModel);
                    }
                    isBuiltNewLine = false;
                    break;
                case States.Calculating:
                    break;
                case States.Loading:
                    break;
                case States.Sending:
                    if (ThrowCtr % refreshPlotCount == 1 & ShRefPlotModel != null & ElRefPlotModel != null)
                    {
                        var shRefPoints = ConvertThrowDataMemberToPoints(robotArmProtocol.ThrowData.Data.Shoulder.Ref);
                        var elRefPoints = ConvertThrowDataMemberToPoints(robotArmProtocol.ThrowData.Data.Elbow.Ref);
                        BuildLineOnPlot(ShRefPlotModel, LineType.ShoulderRef, shRefPoints);
                        BuildLineOnPlot(ElRefPlotModel, LineType.ElbowRef, elRefPoints);
                    }
                    var shCmdPoints = ConvertThrowDataMemberToPoints(robotArmProtocol.ThrowData.Data.Shoulder.Cmd);
                    var elCmdPoints = ConvertThrowDataMemberToPoints(robotArmProtocol.ThrowData.Data.Elbow.Cmd);
                    BuildLineOnPlot(ShCmdPlotModel, LineType.ShoulderCmd, shCmdPoints);
                    BuildLineOnPlot(ElCmdPlotModel, LineType.ElbowCmd, elCmdPoints);
                    break;
                case States.Receiving:
                    break;
                case States.Done:
                    SaveButtonVisibility = Visibility.Visible;
                    RerunIsEnabled = true;
                    break;
                case States.Error:
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Event handler that reacts to incoming sensor data from arduino by plotting points
        /// </summary>
        /// <param name="shData"></param>
        /// <param name="elData"></param>
        private void RobotArmProtocol_updatedDataEvent(List<Point> shData, List<Point> elData)
        {
            if (!isBuiltNewLine)
            {
                BuildLineOnPlot(ShRefPlotModel, LineType.ShoulderSensor, shData);
                BuildLineOnPlot(ElRefPlotModel, LineType.ElbowSensor, elData);
                isBuiltNewLine = true;
            }
            else
            {
                // TODO - need to plot elbow data
                AddPointsToCurrentLine(ShRefPlotModel, shData);
                AddPointsToCurrentLine(ElRefPlotModel, elData);
            }
        }

        #endregion

        #region Helper Methods

        private void SetupPlotModel(PlotModel pm, String chartTitle, String yAxisTitle, int[] minmax)
        {
            pm.LegendTitle = "Legend";
            pm.LegendTitleFontSize = 12;
            pm.LegendTitleColor = OxyColors.DarkGray;
            pm.LegendTextColor = OxyColors.DarkSlateGray;
            pm.LegendOrientation = LegendOrientation.Horizontal;
            pm.LegendPlacement = LegendPlacement.Outside;
            pm.LegendPosition = LegendPosition.TopRight;
            pm.LegendBackground = OxyColors.Black;
            pm.LegendBorder = OxyColors.White;
            pm.IsLegendVisible = false;
            pm.Title = chartTitle;
            pm.TitleColor = OxyColors.Magenta;

            var xAxis = new LinearAxis()
            {
                Position = AxisPosition.Bottom,
                AxislineColor = OxyColors.LightGray,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 3,
                AxisTitleDistance = 20,
                //IsAxisVisible = true,
                AxisDistance = 1, //this must be at least 1
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.DarkGray,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "Time (ms)",
                TitleColor = OxyColors.Magenta,
                AxisTickToLabelDistance = 10,
                TitleFontSize = 15,
                Minimum = minmax[0],
                Maximum = minmax[1],
                TickStyle = TickStyle.Outside,
                TextColor = OxyColors.White,
                FontSize = 10,
                //PositionAtZeroCrossing = true,
            };
            pm.Axes.Add(xAxis);

            var valueAxis = new LinearAxis()
            {
                Position = AxisPosition.Left,
                AxislineColor = OxyColors.LightGray,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 3,
                AxisTitleDistance = 20,
                //IsAxisVisible = true,
                AxisDistance = 1, //this must be at least 1
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.DarkGray,
                MinorGridlineStyle = LineStyle.Dot,
                Title = yAxisTitle,
                TitleColor = OxyColors.Magenta,
                AxisTickToLabelDistance = 10,
                TitleFontSize = 15,
                Minimum = minmax[2],
                Maximum = minmax[3],
                TickStyle = TickStyle.Outside,
                TextColor = OxyColors.White,
                FontSize = 10,
            };
            pm.Axes.Add(valueAxis);
         
        }


        // TODO - unused, might delete
        /*
        private List<Point> GetDataPoints()
        {
            List<Point> output = new List<Point>();
            int[] timeSteps = new int[] {1, 2, 3, 4, 5};
            int[] shoulderAngles = new int[] {100, 130, 3, 4, 5 };
            for (int i = 0; i < timeSteps.Length; i++)
            {
                Point p = new Point(timeSteps[i], shoulderAngles[i]);
                output.Add(p);
            }
            return output;
        }*/

        void AddPointsToCurrentLine(PlotModel pm, List<Point> data)
        {
            var lineSerie = pm.Series[pm.Series.Count - 1] as LineSeries;
            if (lineSerie != null)
            {
                foreach (var point in data)
                {
                    lineSerie.Points.Add(new DataPoint(point.X, point.Y));
                }
            }
            pm.InvalidatePlot(true);
            pm.IsLegendVisible = true;
        }
        private void BuildLineOnPlot(PlotModel pm, LineType lineType, List<Point> data)
        {
            var lineSerie = new LineSeries
            {
                Color = OxyColors.MediumPurple,
                MarkerStroke = OxyColors.MediumPurple,
                CanTrackerInterpolatePoints = false,
            };

            switch (lineType)
            {
                case LineType.ElbowCmd:
                case LineType.ElbowSensor:
                case LineType.ShoulderCmd:
                case LineType.ShoulderSensor:
                    {
                        lineSerie.Title = $"Trial {ThrowCtr}";
                        lineSerie.StrokeThickness = 2;
                        lineSerie.MarkerSize = 1;
                        lineSerie.MarkerType = MarkerType.Circle;

                        switch (ThrowCtr% refreshPlotCount)
                        {
                            case 0:
                                {
                                    lineSerie.Color = OxyColors.Yellow;
                                    lineSerie.MarkerStroke = lineSerie.Color;
                                    break;
                                }
                            case 1:
                                {
                                    lineSerie.Color = OxyColors.Orange;
                                    lineSerie.MarkerStroke = lineSerie.Color;
                                    break;
                                }
                            case 2:
                                {
                                    lineSerie.Color = OxyColors.LimeGreen;
                                    lineSerie.MarkerStroke = lineSerie.Color;
                                    break;
                                }
                            case 3:
                                {
                                    lineSerie.Color = OxyColors.Red;
                                    lineSerie.MarkerStroke = lineSerie.Color;
                                    break;
                                }
                            case 4:
                                {
                                    lineSerie.Color = OxyColors.Blue;
                                    lineSerie.MarkerStroke = lineSerie.Color;
                                    break;
                                }
 
                            default:
                                {
                                    Random r = new Random();
                                    lineSerie.Color = OxyColor.FromRgb((byte)r.Next(0, 256),
                                             (byte)r.Next(0, 256), (byte)r.Next(0, 256));
                                    lineSerie.MarkerStroke = lineSerie.Color;
                                    break;
                                }

                        }
                        break;
                    }
                case LineType.ElbowRef:
                case LineType.ShoulderRef:
                    {
                        lineSerie.Title = "Ref";
                        lineSerie.StrokeThickness = 3;
                        lineSerie.MarkerSize = 1;
                        lineSerie.MarkerType = MarkerType.Star;
                        lineSerie.LineStyle = LineStyle.Dash;
                        lineSerie.Color = OxyColors.White;
                        lineSerie.MarkerStroke = lineSerie.Color;
                        break;
                    }   
            }

            foreach (var point in data)
            {
                lineSerie.Points.Add(new DataPoint(point.X, point.Y));
            }

            pm.Series.Add(lineSerie);
            pm.InvalidatePlot(true);
            pm.IsLegendVisible = true;
        }
        void ClearPlot(PlotModel pm)
        {
            pm.Series.Clear();
            pm.InvalidatePlot(true);
        }
        private List<Point> ConvertThrowDataMemberToPoints(float[] dm)
        {
            var intArray = ArrayConverter.ConvertFloatArrayToIntArray(dm);
            var timeArray = ConvertTimeToMillis(robotArmProtocol.Time); // TODO - this needs to be replaced
            var points = ArrayConverter.ConvertIntArraysToPointList(timeArray, intArray);
            return points;
        }

        private int[] ConvertTimeToMillis(float[] time)
        {
            var output = new int[time.Length];
            for(int i=0; i< time.Length; i++)
            {
                int x = (int)Math.Round(time[i] * 1000.0);
                output[i] = x;
            }
            return output;
        }

        #endregion

        #region UI Actions

        public void StartButton()
        {

            robotArmProtocol.ThrowRequested = true;

        }
        public void SaveButton()
        {
            robotArmProtocol.SavePythonJson();
            SaveButtonImage = saveIconGreen;
            ColorIsMouseOver = Brushes.Transparent;
        }

        #endregion
    }
}