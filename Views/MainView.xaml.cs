using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using SleepTracker.ViewModels;

using MHApi.GUI;

namespace SleepTracker.Views
{
    

    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : WindowAwareView
    {
        MainViewModel _viewModel;

        public MainView()
        {
            InitializeComponent();
            _viewModel = ViewModel.Source as MainViewModel;
        }

        #region Cleanup

        protected override void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel != null)
                _viewModel.Dispose();
            base.WindowClosing(sender, e);
        }

        #endregion
    }

}
