using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using ipp;

namespace SleepTracker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            MHApi.Utilities.DispatcherHelper.Initialize();
            core.ippInit();
        }
    }
}
