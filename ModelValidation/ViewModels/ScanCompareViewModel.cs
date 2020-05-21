using Microsoft.Win32;
using ModelValidation.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ModelValidation.ViewModels
{


    public class ScanCompareViewModel: BindableBase
    {
        private PlanSetup selectedPlan;

        public PlanSetup SelectedPlan {
            get { return selectedPlan; }
            set {
                SetProperty(ref selectedPlan, value);
                CompareScanCommand.RaiseCanExecuteChanged();
            }
        }
        private string scanDetails;
        private IEventAggregator _eventAggregator;

        public string ScanDetails {
            get { return scanDetails; }
            set { SetProperty(ref scanDetails, value); }
        }
        public List<DataScan> ds_list { get; private set; }
        public DelegateCommand OpenScanCommand { get; private set; }
        public DelegateCommand CompareScanCommand { get; private set; }
        public ScanCompareViewModel(IEventAggregator eventAggregator) {
            _eventAggregator = eventAggregator;
            ds_list = new List<DataScan>();
            _eventAggregator.GetEvent<PlanSelectedEvent>().Subscribe(OnPlanSelected);
            OpenScanCommand = new DelegateCommand(OnOpenScan);
            CompareScanCommand = new DelegateCommand(OnCompareScan, CanCompareScan);
        }

        private void OnCompareScan() {



            //prevScans_sp.Children.Clear();//we will refill this stackpanel with results later.
            PlanSetup ps = SelectedPlan;
            foreach (DataScan ds in ds_list) {
                //find a beam with the same field size as the scan.
                string s_output = "";
                Beam b_keep = null;
                foreach (Beam b in ps.Beams) {
                    double x1 = b.ControlPoints.First().JawPositions.X1;
                    double x2 = b.ControlPoints.First().JawPositions.X2;
                    double y1 = b.ControlPoints.First().JawPositions.Y1;
                    double y2 = b.ControlPoints.First().JawPositions.Y2;
                    if (Math.Abs(x2 - x1 - ds.FieldX) < 0.1 && Math.Abs(y2 - y1 - ds.FieldY) < 0.1) {
                        b_keep = b;//found a winner!
                        break;
                    }
                }
                if (b_keep == null) { s_output = "No Scan"; }//could not find the similar field.
                else {
                    //analyze the field.
                    //start by gettig the dose profile.
                    VVector start = new VVector();
                    start.x = ds.axisDir == "X" ? ds.scan_data.First().Item1 : 0;
                    start.y = ds.axisDir == "X" ? ds.depth - 200 : ds.scan_data.First().Item1 - 200;//-200 because of the conversion from depth to dicom coordinate.
                    start.z = 0; //crossline profiles only!
                    VVector end = new VVector();
                    end.x = ds.axisDir == "X" ? ds.scan_data.Last().Item1 : start.x;
                    end.y = ds.axisDir == "X" ? start.y : ds.scan_data.Last().Item1 - 200;
                    end.z = start.z;
                    double[] size = new double[ds.scanLength];//make data arrays the same size.
                    DoseProfile dp = b_keep.Dose.GetDoseProfile(start, end, size);
                    //normalization factor (eclipse calcs not normalized)
                    double norm_factor = ds.axisDir == "X" ? //if this is a profile
                        dp.Where(x => x.Position.x >= 0).First().Value : //norm to central axis.
                        dp.Max(x => x.Value);//for PDD normalizeto max dose.
                                             //write the data to the desktop 
                    using (StreamWriter sw = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                        "\\Scan" + ds.FieldX.ToString() + "X" + ds.FieldY.ToString() + "_" + ds.depth.ToString() + ".csv")) {
                        sw.WriteLine("Scan Pos, Scan Dose, Calc Pos, Calc Dose, Gamma");
                        foreach (Tuple<double, double> tdd in ds.scan_data) {
                            //check the profile point to make sure a point exists in the doseprofile.
                            IEnumerable<ProfilePoint> pp_check = ds.axisDir == "X" ?
                                dp.Where(x => x.Position.x >= tdd.Item1) :
                                dp.Where(x => x.Position.y+200 >= tdd.Item1);
                            ProfilePoint pp;
                            //if there is not profile point, then you cannot do that gamma analysis.
                            if (pp_check.Count() == 0) { break; } else { pp = pp_check.First(); }//but if there is a profile point, take the first.
                                                                                                 //get calculated position and dose (from eclipse profile)
                            string calc_pos = ds.axisDir == "X" ?
                                pp.Position.x.ToString() :
                                Convert.ToString(pp.Position.y + 200);
                            string calcdos = Convert.ToString(pp.Value / norm_factor * 100);
                            double gam = Get_Gamma(dp, tdd, Convert.ToDouble(calc_pos), Convert.ToDouble(calcdos), pp, ds.axisDir, norm_factor);
                            //write data to csv
                            sw.WriteLine(String.Format("{0},{1},{2},{3},{4}",
                                tdd.Item1, tdd.Item2, calc_pos, calcdos, gam));
                        }
                        sw.Flush();
                        s_output = "Success";
                    }
                }

            }

        }



        private double Get_Gamma(DoseProfile dp, Tuple<double, double> tdd, double v1, double v2, ProfilePoint pp, string axisDir, double norm_factor) {
            //throw new NotImplementedException();
            List<double> gamma_values = new List<double>();
            double dd = 0.02 * norm_factor;//2%
            double dta = 2; //2mm 
            int loc = dp.ToList().IndexOf(pp);
            //search backward 10* the dta
            int start = loc - 10 * dta < 0 ? 0 : loc - Convert.ToInt16(10 * dta);
            int end = loc + 10 * dta > dp.Count() ? dp.Count() : loc + Convert.ToInt16(10 * dta);
            for (double i = start; i < end - 1; i += 0.1) {
                int r0 = (int)Math.Floor(i);
                int r1 = (int)Math.Ceiling(i);
                //find the position in the doseprofile.
                double x0 = axisDir == "X" ? dp[r0].Position.x : dp[r0].Position.y + 200;
                double x1 = axisDir == "X" ? dp[r1].Position.x : dp[r1].Position.y + 200;
                //find the value in the dose profeil.
                double y0 = dp[r0].Value;
                double y1 = dp[r1].Value;
                double dos = 0;
                double pos = 0;
                if (r0 == r1) {
                    //cannot linearly interpolate between the same number
                    pos = x0;
                    dos = y0;
                } else {
                    pos = x0 + (i - r0) * (x1 - x0) / (r1 - r0);
                    dos = y0 + (i - r0) * (y1 - y0) / (r1 - r0);
                }
                double gamma = Math.Sqrt(Math.Pow((pos - tdd.Item1) / dta, 2) + Math.Pow((dos / norm_factor * 100 - tdd.Item2) / dd, 2));
                gamma_values.Add(gamma);
            }
            //return the minumum gamma value.
            return gamma_values.Min();
        }


        private bool CanCompareScan() {
            return SelectedPlan != null;
        }

        private void OnOpenScan() {
            ds_list.Clear();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".asc";
            ofd.Filter = "Ascii Files (*.asc)|*.asc|Text Files(*.txt)|*.txt|W2CAD Files (*.cdp)|*.cdp";
            if (ofd.ShowDialog() == true) {
                //read out content here.
                foreach (string line in File.ReadAllLines(ofd.FileName)) {
                    if (line.Contains("STOM")) {
                        ds_list.Add(new DataScan());
                    }
                    if (line.Contains("FLSZ")) {
                        ds_list.Last().FieldX = Convert.ToDouble(line.Split(' ').Last().Split('*').First());
                        ds_list.Last().FieldY = Convert.ToDouble(line.Split('*').Last());
                    }
                    if (line.Contains("TYPE")) {
                        switch (line.Split(' ').Last()) {
                            case "OPP":
                                ds_list.Last().axisDir = "X";
                                break;
                            case "OPD":
                                ds_list.Last().axisDir = "Z";
                                break;
                            case "DPR":
                                ds_list.Last().axisDir = "X";//for now
                                break;
                        }
                    }
                    if (line.Contains("PNTS")) {
                        ds_list.Last().scanLength = Convert.ToInt32(line.Split(' ').Last());
                    }
                    if (line.Contains("STEP")) {
                        ds_list.Last().stepSize = Convert.ToDouble(line.Split(' ').Last());
                    }
                    if (line.Contains("DPTH")) {
                        ds_list.Last().depth = Convert.ToDouble(line.Split(' ').Last());
                    }
                    if (line[0] == '<') {
                        //this is where the beam data exists
                        double pos = ds_list.Last().axisDir == "X" ? //if the direction is x (profile)
                            Convert.ToDouble(line.Split(' ').First().Trim('<')) ://take the first value (x)
                            Convert.ToDouble(line.Split(' ')[2]);//else take the 3rd value (z)
                        double val = Convert.ToDouble(line.Split(' ').Last().Trim('>'));
                        ds_list.Last().scan_data.Add(new Tuple<double, double>(pos, val));
                    }
                }
                //preview scan data in stackpanel
                ScanDetails = String.Empty;
                foreach (DataScan ds in ds_list) {

                    string scan_type = ds.axisDir == "X" ? "Profile" : "PDD";
                    string depth_type = ds.axisDir == "X" ? ds.depth.ToString() : "NA";
                    ScanDetails += $"{scan_type}-FLSZ({ds.FieldX}x{ds.FieldY}) -Depth({depth_type})";
                }
            }
        }

        private void OnPlanSelected(PlanSetup obj) {
            SelectedPlan = obj;
        }
    }

    public class DataScan
    {
        internal string axisDir;
        internal int scanLength;
        internal double stepSize;
        internal double depth;

        public double FieldX { get; internal set; }
        public double FieldY { get; internal set; }
        public List<Tuple<double, double>> scan_data { get; set; }
        public DataScan() {
            scan_data = new List<Tuple<double, double>>();
        }
    }
}
