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



namespace WPFUI.ViewModels
{
    class ThrowingViewModel : Screen
    {
        #region Defines

        private PlotModel plotModel;
        private int refreshPlotCount = 3; //amount of trials to run until the plot is refreshed
        private PlotModel cmdPlotModel;
        SerialClient serialClient;
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
  

        #endregion  

        #region Constructors
        public ThrowingViewModel()
        {
            if (PlotModel == null)
            {
                PlotModel = new PlotModel();
                int[] minmax = { 0, 2010, -20, 200 };
                SetupPlotModel(PlotModel, "Joint Trajectory vs. Time","Angle (deg)", minmax);
            }

            if (CmdPlotModel == null)
            {
                CmdPlotModel = new PlotModel();
                int[] minmax = { 0, 2010, -50, 50 };
                SetupPlotModel(CmdPlotModel, "Cmd Input vs. Time", "Cmd (psi)",minmax);
            }
        }

        public ThrowingViewModel(SerialClient sc)
            : this()
        {
            serialClient = sc;
            serialClient.RobotArmProtocol.refreshPlotCount = refreshPlotCount;
            serialClient.RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
            serialClient.RobotArmProtocol.updatedDataEvent += RobotArmProtocol_updatedDataEvent;
            serialClient.RobotArmProtocol.referenceDataEvent += RobotArmProtocol_referenceDataEvent;
            serialClient.RobotArmProtocol.commandDataEvent += RobotArmProtocol_commandDataEvent;
            
        }

        #endregion

        #region Event Handlers
        private void RobotArmProtocol_stateChangedEvent(string message)
        {
            switch (serialClient.RobotArmProtocol.state)
            {
                case States.OnStartup:
                    break;
                case States.Initialized:
                    break;
                case States.Idle:
                    break;
                case States.TaskPlanning:
                    break;
                case States.Sending:
                    break;
                case States.Starting:
                    break;
                case States.Receiving:
                    break;
                case States.Done:
                    StartButtonVisibility = Visibility.Visible;
                    break;
                case States.Error:
                    break;
                default:
                    break;
            }
        }

        private void RobotArmProtocol_referenceDataEvent(List<Point> data)
        {
            ClearPlot(PlotModel);
            ClearPlot(CmdPlotModel);
            BuildLineOnPlot(PlotModel, LineType.ShoulderRef, data);
        }
        private void RobotArmProtocol_updatedDataEvent(List<Point> data)
        {
            if (!isBuiltNewLine)
            {
                //BuildLineOnPlot(PlotModel, _serialSelectionViewModel.GetDataPoints());
                BuildLineOnPlot(PlotModel, LineType.ShoulderSensor, data);
                isBuiltNewLine = true;
            }
            else
            {
                AddPointsToCurrentLine(PlotModel, data);
            }
        }

        /// <summary>
        /// Plots command data
        /// </summary>
        /// <param name="data"></param>
        private void RobotArmProtocol_commandDataEvent(List<Point> data)
        {
             BuildLineOnPlot(CmdPlotModel, LineType.ShoulderCmd, data);
        }



        #endregion

        #region Properties

        public PlotModel PlotModel
        {
            get 
            { 
                return plotModel; 
            }
            set 
            { 
                plotModel = value; 
                NotifyOfPropertyChange(() => PlotModel);
            }
        }
        public PlotModel CmdPlotModel
        {
            get
            {
                return cmdPlotModel;
            }
            set
            {
                cmdPlotModel = value;
                NotifyOfPropertyChange(() => CmdPlotModel);
            }
        }
        public int ThrowCtr
        {
            get
            {
                return serialClient.RobotArmProtocol.ThrowCtr;
            }
            set
            {
                serialClient.RobotArmProtocol.ThrowCtr = value;
            }
        }
        private Visibility _startButtonVisibility;
        public Visibility StartButtonVisibility
        {
            get
            {
                return _startButtonVisibility;
            }
            set
            {
                _startButtonVisibility = value;
                NotifyOfPropertyChange(() => StartButtonVisibility);
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
        }
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
            //Console.WriteLine("Building Plot");
            var lineSerie = new LineSeries
            {
                Color = OxyColors.MediumPurple,
                MarkerStroke = OxyColors.MediumPurple,
                CanTrackerInterpolatePoints = false,
            };

            switch (lineType)
            {
                case LineType.ShoulderCmd:
                case LineType.ShoulderSensor:
                    {
                        lineSerie.Title = $"Shoulder Trial {ThrowCtr}";
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
                case LineType.ShoulderRef:
                    {
                        lineSerie.Title = "Shoulder Ref";
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

        #endregion

        #region UI Actions
        public void StartButton()
        {
            StartButtonVisibility = Visibility.Hidden;

            serialClient.RobotArmProtocol.ThrowRequested = true;
            serialClient.RobotArmProtocol.UpdateStateMachine();
            
            isBuiltNewLine = false;
            //ClearPlot();
            
            
        }
 


        #endregion
    }
}