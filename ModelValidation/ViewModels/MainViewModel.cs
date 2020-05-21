using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelValidation.ViewModels
{
    public class MainViewModel {
        public MainViewModel(NavigationViewModel navigationViewModel, ScanCompareViewModel scanCompareViewModel) {
                NavigationViewModel = navigationViewModel;
                ScanCompareViewModel = scanCompareViewModel;
            }
        public NavigationViewModel NavigationViewModel { get; private set; }
        public ScanCompareViewModel ScanCompareViewModel { get; private set; }
    }
}
