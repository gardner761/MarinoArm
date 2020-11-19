using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SerialLibrary;
using static SerialLibrary.RobotArmProtocol;
using System.Windows.Media.Animation;
using System.Diagnostics;

namespace WPFUI.ViewModels
{
    /// <summary>
    /// This class is responsible for manageing the UI Shell (frame that holds other windows)
    /// </summary>
    public class ShellViewModel : Conductor<object>
    {
        #region Defines

        private Screen pageOneViewModel;
        private SerialSelectionViewModel serialSelectionViewModel;
        private Screen throwingViewModel;

        #endregion

        #region Properties

        private SolidColorBrush _statusBarColor = Brushes.Black;
        public SolidColorBrush StatusBarColor
        {
            get
            {
                return _statusBarColor;
            }
            set
            {
                _statusBarColor = value;
                NotifyOfPropertyChange(() => StatusBarColor);
            }
        }

        private double _statusBarOpacity;

        public double StatusBarOpacity
        {
            get { return _statusBarOpacity; }
            set
            {
                _statusBarOpacity = value;
                NotifyOfPropertyChange(() => StatusBarOpacity);
            }
        }


        private Visibility _throwingButtonVisibility;
        public Visibility ThrowingButtonVisibility
        {
            get
            {
                return _throwingButtonVisibility;
            }
            set
            {
                _throwingButtonVisibility = value;
                NotifyOfPropertyChange(() => ThrowingButtonVisibility);
            }
        }


        private bool isAnimationRunning;
        public bool IsAnimationRunning
        {
            get 
            { 
                return isAnimationRunning; 
            }

            set 
            { 
                isAnimationRunning = value;
                NotifyOfPropertyChange(() => IsAnimationRunning);
            }
        }


        #endregion

        #region Constructors

        public ShellViewModel()
        {
            ShowSerialSelectionPage();
            ThrowingButtonVisibility = Visibility.Hidden;
            //AnimateColorBar();
            //StatusBarColor = Brushes.Orange;  
        }

        #endregion

        #region Event Handlers
        private void RobotArmProtocol_stateChangedEvent(string message)
        {

            ConvertStateToStatusColor(serialSelectionViewModel.RobotArmProtocol.state);
            if (serialSelectionViewModel.RobotArmProtocol.state == States.Idle)
            {
                ThrowingButtonVisibility = Visibility.Visible;
            }
        }

        private void SerialSelectionViewModel_connectionMadeEvent()
        {
            serialSelectionViewModel.RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
        }
        #endregion

        #region Show Page Methods
 
        public void ShowPageOne() //Bound to UI Button Click via Caliburn Micro Naming Convention
        {
            if (pageOneViewModel == null)
            {
                pageOneViewModel = new PageOneViewModel();
            }
            ActivateItem(pageOneViewModel);
        }

        public void ShowSerialSelectionPage() //Bound to UI Button Click via Caliburn Micro Naming Convention
        {
            if (serialSelectionViewModel == null)
            {
                serialSelectionViewModel = new SerialSelectionViewModel();
                serialSelectionViewModel.connectionMadeEvent += SerialSelectionViewModel_connectionMadeEvent;
            }
            ActivateItem(serialSelectionViewModel);
            //ThrowingButtonVisibility = Visibility.Visible;
        }

        public void ShowThrowingPage() //Bound to UI Button Click via Caliburn Micro Naming Convention
        {
            
                if (serialSelectionViewModel == null)
                {
                    throwingViewModel = new ThrowingViewModel();
                }
                else
                {
                    throwingViewModel = new ThrowingViewModel(serialSelectionViewModel.RobotArmProtocol);
                }
            ActivateItem(throwingViewModel);
        }

        #endregion

        private void ConvertStateToStatusColor(States rapState)
        {
            switch (rapState)
            {
                case States.Idle:
                    {
                        StatusBarColor = Brushes.Black;
                        break;
                    }
                case States.Calculating:
                    {
                        StatusBarColor = Brushes.BlueViolet;
                        IsAnimationRunning = true;
                        break;
                    }
                case States.Sending:
                    {
                        //StatusBarColor = Brushes.Orange;
                        IsAnimationRunning = true;
                        break;
                    }
                case States.Receiving:
                    {
                        break;
                    }
                case States.Done:
                    {
                        StatusBarColor = Brushes.LimeGreen;
                        IsAnimationRunning = false;
                        break;
                    }
                case States.Error:
                    {
                        StatusBarColor = Brushes.Red;
                        IsAnimationRunning = false;
                        break;
                    }
            }
        }
        public override void TryClose(bool? dialogResult = null)
        {
            Dispose();
            base.TryClose(dialogResult);
        }

        public void Dispose()
        {
            if (serialSelectionViewModel != null)
            { 
                serialSelectionViewModel.CloseAndDispose();
                Console.WriteLine("Closing and Disposing Serial Selection View");
            }

        }
        protected override void OnDeactivate(bool close)
        {
            try
            {
                Dispose();
            }
            catch
            { }
            if(CheckForActiveThreads())
            {
                throw new Exception("More than one active thread preventing application close");
            }
            
            //System.Environment.Exit(0);
        }

        
        private bool CheckForActiveThreads()
        {
            bool output = false;
            int activeThreadCtr = 0;
            ProcessThreadCollection currentThreads = Process.GetCurrentProcess().Threads;

            foreach (ProcessThread thread in currentThreads)
            {
                if (thread.ThreadState == ThreadState.Running)
                {
                    activeThreadCtr++;
                    if (activeThreadCtr > 1)
                    { 
                        return output = true;
                    }
                }
            }
            return output;
        }



    }
}