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
        private SerialClient serialClient;

        #endregion

        #region Properties

        private SolidColorBrush _statusBarColor;
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

            ConvertStateToStatusColor(serialSelectionViewModel.SerialClient.RobotArmProtocol.state);
            if (serialSelectionViewModel.SerialClient.RobotArmProtocol.state == States.Idle)
            {
                ThrowingButtonVisibility = Visibility.Visible;
            }
        }

        private void SerialSelectionViewModel_connectionMadeEvent()
        {
            serialSelectionViewModel.SerialClient.RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
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
                    serialClient = serialSelectionViewModel.SerialClient;
                    throwingViewModel = new ThrowingViewModel(serialClient);
                    //_serialSelectionViewModel.SerialClient.RobotArmProtocol.stateChangedEvent += RobotArmProtocol_stateChangedEvent;
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
                        serialSelectionViewModel.SerialClient.RobotArmProtocol.UpdateStateMachine();
                        break;
                    }
                case States.Starting:
                    {
                        StatusBarColor = Brushes.Orange;
                        IsAnimationRunning = true;
                        break;
                    }
                case States.Receiving:
                    {
                        break;
                    }
                case States.Done:
                    {
                        //StatusBarColor = Brushes.LimeGreen;
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

        }
    }
}