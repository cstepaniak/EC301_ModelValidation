using ModelValidation.ViewModels;
using ModelValidation.Views;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;

[assembly:ESAPIScript(IsWriteable = true)]  // we need this for a writeable script
namespace ModelValidation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App: System.Windows.Application
    {
        private VMS.TPS.Common.Model.API.Application _app;
        private MainView mv;

        private void Application_Startup(object sender, StartupEventArgs e) {
            try {
                using (_app = VMS.TPS.Common.Model.API.Application.CreateApplication()) {
                    IEventAggregator eventAggregator = new EventAggregator();
                    mv = new MainView();
                    mv.DataContext = new MainViewModel(
                        new NavigationViewModel(_app,eventAggregator),
                        new ScanCompareViewModel(eventAggregator));
                    mv.ShowDialog();
                }
            } catch (ApplicationException ex) {
                // give the exception to the user somehow (log file, messagebox, etc)
                _app.ClosePatient();  //this is like closing the patient in Eclipse
                _app.Dispose();  //This is like closing Eclipse
            }
        }
    }
}
