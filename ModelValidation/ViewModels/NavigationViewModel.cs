using ModelValidation.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ModelValidation.ViewModels
{
    public class NavigationViewModel:BindableBase {
        private Application _app;  //Convention: name the inputs from the constructor with a _
        private IEventAggregator _eventAggregator;
        private string patientId;
        public string PatientId {
            get { return patientId; }
            set {
                SetProperty(ref patientId, value);
                OpenPatientCommand.RaiseCanExecuteChanged(); //Fires CanOpenPatient()
            }
        }
        private Course selectedCourse;
        public Course SelectedCourse {
            get { return selectedCourse; }
            set {
                SetProperty(ref selectedCourse, value);
                GetPlans();
            }
        }

        private PlanSetup selectedPlan;
        public PlanSetup SelectedPlan {
            get { return selectedPlan; }
            set {
                SetProperty(ref selectedPlan, value);
                if (SelectedPlan != null) {
                    _eventAggregator.GetEvent<PlanSelectedEvent>().Publish(SelectedPlan);
                }
            }
        }
        private Patient currentPatient;
        public Patient CurrentPatient {
            get { return currentPatient; }
            set {
                SetProperty(ref currentPatient, value);
                GeneratePlanCommand.RaiseCanExecuteChanged(); //Fires CanGeneratePlan()
            }
        }

        //Collections
        public ObservableCollection<Course> Courses { get; private set; }
        public ObservableCollection<PlanSetup> Plans { get; private set; }

        //commands
        public DelegateCommand OpenPatientCommand { get; private set; }
        public DelegateCommand GeneratePlanCommand { get; private set; }

        //constructor
        public NavigationViewModel(Application app, IEventAggregator eventAggregator) {
            _app = app;
            _eventAggregator = eventAggregator;
            Courses = new ObservableCollection<Course>();
            Plans = new ObservableCollection<PlanSetup>();
            OpenPatientCommand = new DelegateCommand(OnOpenPatient, CanOpenPatient);
            GeneratePlanCommand = new DelegateCommand(OnGeneratePlan, CanGeneratePlan);

        }

        private bool CanGeneratePlan() {
            return CurrentPatient != null; // || CurrentPatient.StructureSets.Count() > 0
        }

        private void OnGeneratePlan() {
            // This will create a bunch of beams on a 40x40 cm box phantom, with user orign in the exact center (as well as the DICOM origin)
            //Patient Coordinate system:
            // X increases toward patient left
            // y increases toward patient Posterior
            // z increases toward patient inferior
            // and follows patient position


            // System.Windows.MessageBox.Show("Plan Generation Failed");
            string _machineId = ConfigurationManager.AppSettings["machine"];
            CurrentPatient.BeginModifications();
            var autoCourse = CurrentPatient.AddCourse();
            if (CurrentPatient.StructureSets.Count() > 0) {
                var autoPlan = autoCourse.AddExternalPlanSetup(CurrentPatient.StructureSets.FirstOrDefault());
                ExternalBeamMachineParameters machineParameters = new ExternalBeamMachineParameters(
                    _machineId,
                    "6X",
                    600,
                    "STATIC",
                    null);
                double[] fs = new double[] { 40.0, 60.0, 80.0, 100.0, 200.0 };
                foreach (var field in fs) {
                    double jaw = field / 2.0;
                    // add beams (in IEC 1217 DICOM standard)
                    autoPlan.AddStaticBeam(machineParameters,
                        new VRect<double>(-1.0 * jaw, -1.0 * jaw, jaw, jaw),
                        0,
                        0,
                        0,
                        new VVector(0, -200.0, 0)
                        );
                }
                autoPlan.SetPrescription(1, new DoseValue(100, DoseValue.DoseUnit.cGy), 1.0);
                autoPlan.CalculateDose();
                _app.SaveModifications(); //only needed for standalone applications
                Courses.Add(autoCourse);
                SelectedCourse = autoCourse;
                SelectedPlan = autoPlan;

            }
        }

        private bool CanOpenPatient() {
            return !String.IsNullOrEmpty(PatientId);
        }

        private void OnOpenPatient() {
            _app.ClosePatient();
            CurrentPatient = _app.OpenPatientById(PatientId);
            Courses.Clear();
            if (CurrentPatient != null) {
                foreach (var course in CurrentPatient.Courses) {
                    Courses.Add(course);
                }
            }
        }
        private void GetPlans() {
            Plans.Clear();
            if (SelectedCourse != null) {
                foreach (var plan in SelectedCourse.PlanSetups) {
                    Plans.Add(plan);
                }
            }
        }
    }
}
