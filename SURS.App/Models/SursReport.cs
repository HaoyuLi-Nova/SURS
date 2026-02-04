using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace SURS.App.Models
{
    public class SursReport : ObservableObject
    {
        public SursReport()
        {
            InitializeDateSelections();
            InitializeAdnexaRegions();
        }

        // Image Paths
        public ObservableCollection<string> ImagePaths { get; } = new ObservableCollection<string>();

        // Date Selection Helpers
        private ObservableCollection<int> _availableYears = new ObservableCollection<int>();
        public ObservableCollection<int> AvailableYears 
        { 
            get => _availableYears; 
            set => SetProperty(ref _availableYears, value);
        }

        private ObservableCollection<int> _availableMonths = new ObservableCollection<int>();
        public ObservableCollection<int> AvailableMonths 
        { 
            get => _availableMonths; 
            set => SetProperty(ref _availableMonths, value);
        }

        private ObservableCollection<int> _availableDays = new ObservableCollection<int>();
        public ObservableCollection<int> AvailableDays 
        { 
            get => _availableDays; 
            set => SetProperty(ref _availableDays, value);
        }
        
        private int? _lmpYear;
        public int? LmpYear
        {
            get => _lmpYear;
            set 
            {
                if (SetProperty(ref _lmpYear, value))
                {
                    UpdateAvailableDays();
                    UpdateLmpFromComponents();
                }
            }
        }

        private int? _lmpMonth;
        public int? LmpMonth
        {
            get => _lmpMonth;
            set 
            {
                if (SetProperty(ref _lmpMonth, value))
                {
                    UpdateAvailableDays();
                    UpdateLmpFromComponents();
                }
            }
        }

        private int? _lmpDay;
        public int? LmpDay
        {
            get => _lmpDay;
            set 
            {
                if (SetProperty(ref _lmpDay, value))
                {
                    UpdateLmpFromComponents();
                }
            }
        }

        private bool _isUpdatingDate;

        private void InitializeDateSelections()
        {
            // Years: 1950 to Next Year + 1
            var currentYear = System.DateTime.Now.Year;
            AvailableYears.Clear();
            for (int i = 1950; i <= currentYear + 2; i++)
            {
                AvailableYears.Add(i);
            }
            
            // Months: 1-12
            AvailableMonths.Clear();
            for (int i = 1; i <= 12; i++)
            {
                AvailableMonths.Add(i);
            }
            
            UpdateAvailableDays();
        }

        private void UpdateAvailableDays()
        {
            var daysInMonth = 31;
            if (LmpYear.HasValue && LmpMonth.HasValue)
            {
                try
                {
                    daysInMonth = System.DateTime.DaysInMonth(LmpYear.Value, LmpMonth.Value);
                }
                catch { }
            }

            if (AvailableDays.Count != daysInMonth)
            {
                if (AvailableDays.Count < daysInMonth)
                {
                    for (int i = AvailableDays.Count + 1; i <= daysInMonth; i++)
                    {
                        AvailableDays.Add(i);
                    }
                }
                else
                {
                    for (int i = AvailableDays.Count; i > daysInMonth; i--)
                    {
                        AvailableDays.RemoveAt(i - 1);
                    }
                }
            }
            
            // If current selected day is out of range, reset it
            if (LmpDay.HasValue && LmpDay.Value > daysInMonth)
            {
                LmpDay = null;
            }
        }

        private void UpdateLmpFromComponents()
        {
            if (_isUpdatingDate) return;
            
            if (LmpYear.HasValue && LmpMonth.HasValue && LmpDay.HasValue)
            {
                try 
                {
                    _isUpdatingDate = true;
                    LastMenstrualPeriod = new System.DateTime(LmpYear.Value, LmpMonth.Value, LmpDay.Value);
                }
                catch
                {
                    // Invalid date, ensure consistency
                    LastMenstrualPeriod = null;
                }
                finally
                {
                    _isUpdatingDate = false;
                }
            }
            else
            {
                 _isUpdatingDate = true;
                 LastMenstrualPeriod = null;
                 _isUpdatingDate = false;
            }
        }

        // Patient Info & Header
        private string _hospitalName = "首都医科大学附属北京妇产医院";
        public string HospitalName 
        { 
            get => _hospitalName;
            set => SetProperty(ref _hospitalName, value);
        }

        private string _patientName = string.Empty;
        public string PatientName 
        { 
            get => _patientName;
            set => SetProperty(ref _patientName, value);
        }

        private string _registrationNo = string.Empty;
        public string RegistrationNo 
        { 
            get => _registrationNo;
            set => SetProperty(ref _registrationNo, value);
        }

        private string _serialNo = string.Empty;
        public string SerialNo 
        { 
            get => _serialNo;
            set => SetProperty(ref _serialNo, value);
        }

        private string _gender = "女";
        public string Gender 
        { 
            get => _gender;
            set => SetProperty(ref _gender, value);
        }

        private string _age = string.Empty;
        public string Age 
        { 
            get => _age;
            set => SetProperty(ref _age, value);
        }

        private string _department = string.Empty;
        public string Department 
        { 
            get => _department;
            set => SetProperty(ref _department, value);
        }

        private string _outpatientNo = string.Empty;
        public string OutpatientNo 
        { 
            get => _outpatientNo;
            set => SetProperty(ref _outpatientNo, value);
        }

        private string _inpatientNo = string.Empty;
        public string InpatientNo 
        { 
            get => _inpatientNo;
            set => SetProperty(ref _inpatientNo, value);
        }

        private string _bedNo = string.Empty;
        public string BedNo 
        { 
            get => _bedNo;
            set => SetProperty(ref _bedNo, value);
        }

        private string _applyingPhysician = string.Empty;
        public string ApplyingPhysician 
        { 
            get => _applyingPhysician;
            set => SetProperty(ref _applyingPhysician, value);
        }

        private string _examItem = "经阴道彩色多普勒超声检查";
        public string ExamItem 
        { 
            get => _examItem;
            set => SetProperty(ref _examItem, value);
        }

        private bool _isEastBranch;
        public bool IsEastBranch 
        { 
            get => _isEastBranch;
            set => SetProperty(ref _isEastBranch, value);
        }

        private bool _isWestBranch;
        public bool IsWestBranch 
        { 
            get => _isWestBranch;
            set => SetProperty(ref _isWestBranch, value);
        }
        
        private System.DateTime? _lastMenstrualPeriod;
        public System.DateTime? LastMenstrualPeriod 
        {
            get => _lastMenstrualPeriod;
            set => SetProperty(ref _lastMenstrualPeriod, value);
        }

        // Template Selection - 模板选择（是否填写各部分内容）
        private bool _includeUterus = true;
        public bool IncludeUterus
        {
            get => _includeUterus;
            set => SetProperty(ref _includeUterus, value);
        }

        private bool _includeEndometrium = true;
        public bool IncludeEndometrium
        {
            get => _includeEndometrium;
            set => SetProperty(ref _includeEndometrium, value);
        }

        private bool _includeOvary = true;
        public bool IncludeOvary
        {
            get => _includeOvary;
            set => SetProperty(ref _includeOvary, value);
        }

        // Uterus (Form 1)
        public Uterus Uterus { get; } = new Uterus();
        public Endometrium Endometrium { get; } = new Endometrium();
        public UterineCavity Cavity { get; } = new UterineCavity();

        // 卵巢正常/异常
        private string _ovaryEvaluation = string.Empty;
        public string OvaryEvaluation
        {
            get => _ovaryEvaluation;
            set => SetProperty(ref _ovaryEvaluation, value);
        }
        
        public bool IsOvaryNormal
        {
            get => OvaryEvaluation == "正常";
            set
            {
                if (!value) return;
                OvaryEvaluation = "正常";
                OnPropertyChanged(nameof(IsOvaryNormal));
                OnPropertyChanged(nameof(IsOvaryAbnormal));
            }
        }
        
        public bool IsOvaryAbnormal
        {
            get => OvaryEvaluation == "异常";
            set
            {
                if (!value) return;
                OvaryEvaluation = "异常";
                OnPropertyChanged(nameof(IsOvaryNormal));
                OnPropertyChanged(nameof(IsOvaryAbnormal));
            }
        }

        public Ovary LeftOvary { get; } = new Ovary("Left");
        public Ovary RightOvary { get; } = new Ovary("Right");

        // 卵巢/附件（分四个部位分别描述：左卵巢、右卵巢、左附件、右附件）
        public ObservableCollection<AdnexaRegion> AdnexaRegions { get; } = new ObservableCollection<AdnexaRegion>();

        private AdnexaRegion? _selectedAdnexaRegion;
        public AdnexaRegion? SelectedAdnexaRegion
        {
            get => _selectedAdnexaRegion;
            set => SetProperty(ref _selectedAdnexaRegion, value);
        }

        // Findings Categories (Checkboxes in the form) - 异常卵巢选项（多选）
        private bool _hasUnilocularCyst;
        public bool HasUnilocularCyst
        {
            get => _hasUnilocularCyst;
            set => SetProperty(ref _hasUnilocularCyst, value);
        }
        public UnilocularCyst UnilocularCyst { get; } = new UnilocularCyst();

        private bool _hasMultilocularCyst;
        public bool HasMultilocularCyst
        {
            get => _hasMultilocularCyst;
            set => SetProperty(ref _hasMultilocularCyst, value);
        }
        public MultilocularCyst MultilocularCyst { get; } = new MultilocularCyst();

        private bool _hasSolidCyst;
        public bool HasSolidCyst
        {
            get => _hasSolidCyst;
            set => SetProperty(ref _hasSolidCyst, value);
        }
        public SolidCyst SolidCyst { get; } = new SolidCyst();

        private bool _hasSolidMass;
        public bool HasSolidMass
        {
            get => _hasSolidMass;
            set => SetProperty(ref _hasSolidMass, value);
        }
        public SolidMass SolidMass { get; } = new SolidMass();

        // Fluid
        private string _fluidStatus = string.Empty;
        public string FluidStatus
        {
            get => _fluidStatus;
            set => SetProperty(ref _fluidStatus, value);
        }
        
        public bool HasNoFluid
        {
            get => FluidStatus == "无";
            set
            {
                if (!value) return;
                FluidStatus = "无";
                OnPropertyChanged(nameof(HasNoFluid));
                OnPropertyChanged(nameof(HasFluid));
            }
        }
        
        public bool HasFluid
        {
            get => FluidStatus == "有";
            set
            {
                if (!value) return;
                FluidStatus = "有";
                OnPropertyChanged(nameof(HasNoFluid));
                OnPropertyChanged(nameof(HasFluid));
            }
        }
        public ObservableCollection<FluidLocation> FluidLocations { get; } = new ObservableCollection<FluidLocation>();
        
        private bool _hasFluidOther;
        public bool HasFluidOther 
        { 
            get => _hasFluidOther;
            set => SetProperty(ref _hasFluidOther, value);
        }

        private string _fluidOtherLocation = string.Empty;
        public string FluidOtherLocation 
        { 
            get => _fluidOtherLocation;
            set => SetProperty(ref _fluidOtherLocation, value);
        }

        // Conclusion
        private string _oRadsScore = string.Empty;
        public string ORadsScore
        {
            get => _oRadsScore;
            set => SetProperty(ref _oRadsScore, value);
        }
        
        // Form 1 Conclusion
        private string _uterusDescription = string.Empty;
        public string UterusDescription 
        { 
            get => _uterusDescription;
            set => SetProperty(ref _uterusDescription, value);
        }

        private void InitializeAdnexaRegions()
        {
            // 默认创建 4 个一级菜单，允许分别填写"正常/异常"
            AdnexaRegions.Clear();
            AdnexaRegions.Add(new AdnexaRegion("左卵巢"));
            AdnexaRegions.Add(new AdnexaRegion("右卵巢"));
            AdnexaRegions.Add(new AdnexaRegion("左附件"));
            AdnexaRegions.Add(new AdnexaRegion("右附件"));
            SelectedAdnexaRegion = AdnexaRegions.FirstOrDefault();
        }

        // O-RADS自动计算器
        private static readonly Services.ORadsCalculator _oradsCalculator = new Services.ORadsCalculator();

        /// <summary>
        /// 计算并更新自动O-RADS分级
        /// </summary>
        public void CalculateAutoORads()
        {
            var result = _oradsCalculator.CalculateORadsLevel(this);
            AutoORadsResult = result;
            
            // 如果使用自动分级，更新ORadsLevel
            if (UseAutoORads && result != null)
            {
                ORadsLevel = result.LevelString;
            }
            
            OnPropertyChanged(nameof(EffectiveORadsLevel));
            OnPropertyChanged(nameof(AutoORadsResult));
        }
        
        // Endometrium Diagnosis (Multi-select)
        private bool _isEndoHyperplasia;
        public bool IsEndoHyperplasia 
        { 
            get => _isEndoHyperplasia;
            set => SetProperty(ref _isEndoHyperplasia, value);
        }

        private bool _isEndoPolyp;
        public bool IsEndoPolyp 
        { 
            get => _isEndoPolyp;
            set => SetProperty(ref _isEndoPolyp, value);
        }

        private bool _isEndoCancer;
        public bool IsEndoCancer 
        { 
            get => _isEndoCancer;
            set => SetProperty(ref _isEndoCancer, value);
        }

        private bool _isSubmucosalMyoma;
        public bool IsSubmucosalMyoma 
        { 
            get => _isSubmucosalMyoma;
            set => SetProperty(ref _isSubmucosalMyoma, value);
        }

        private bool _isEndoOther;
        public bool IsEndoOther 
        { 
            get => _isEndoOther;
            set => SetProperty(ref _isEndoOther, value);
        }

        private string _endoOtherText = string.Empty;
        public string EndoOtherText 
        { 
            get => _endoOtherText;
            set => SetProperty(ref _endoOtherText, value);
        }

        // O-RADS分级
        private string _oRadsLevel = string.Empty;
        public string ORadsLevel
        {
            get => _oRadsLevel;
            set
            {
                // 如果手动设置了O-RADS分级，且与自动分级不同，则禁用自动分级
                if (SetProperty(ref _oRadsLevel, value))
                {
                    if (UseAutoORads && AutoORadsResult != null)
                    {
                        if (value != AutoORadsResult.LevelString)
                        {
                            // 手动选择与自动分级不同，禁用自动分级
                            UseAutoORads = false;
                        }
                    }
                    else if (!string.IsNullOrEmpty(value))
                    {
                        // 手动设置了分级，禁用自动分级
                        UseAutoORads = false;
                    }
                }
            }
        }

        // 自动计算的O-RADS分级结果
        private ORadsResult? _autoORadsResult;
        public ORadsResult? AutoORadsResult
        {
            get => _autoORadsResult;
            set => SetProperty(ref _autoORadsResult, value);
        }

        // 是否使用自动分级（如果为false，则使用手动选择的ORadsLevel）
        private bool _useAutoORads = true;
        public bool UseAutoORads
        {
            get => _useAutoORads;
            set
            {
                if (SetProperty(ref _useAutoORads, value))
                {
                    OnPropertyChanged(nameof(EffectiveORadsLevel));
                }
            }
        }

        // 有效的O-RADS分级（自动或手动）
        public string EffectiveORadsLevel
        {
            get
            {
                if (UseAutoORads && AutoORadsResult != null)
                {
                    return AutoORadsResult.LevelString;
                }
                return ORadsLevel;
            }
        }

        // Footer - 报告签署
        private string _remarks = string.Empty;
        public string Remarks 
        { 
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        private string _typist = string.Empty;
        public string Typist 
        { 
            get => _typist;
            set => SetProperty(ref _typist, value);
        }

        private string _diagnostician = string.Empty;
        public string Diagnostician 
        { 
            get => _diagnostician;
            set => SetProperty(ref _diagnostician, value);
        }
        
        private System.DateTime _reportDate = System.DateTime.Now;
        public System.DateTime ReportDate
        {
            get => _reportDate;
            set => SetProperty(ref _reportDate, value);
        }
    }

    public class Uterus : ObservableObject
    {
        public Uterus()
        {
            // 结节由用户手动添加：默认不创建结节页签
        }

        private string _position = string.Empty;
        public string Position 
        { 
            get => _position;
            set => SetProperty(ref _position, value);
        }

        private double _length;
        public double Length 
        { 
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private double _apDiameter;
        public double APDiameter 
        { 
            get => _apDiameter;
            set => SetProperty(ref _apDiameter, value);
        }

        private double _width;
        public double Width 
        { 
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private double _cervixLength;
        public double CervixLength 
        { 
            get => _cervixLength;
            set => SetProperty(ref _cervixLength, value);
        }

        private double _cervixAP;
        public double CervixAP 
        { 
            get => _cervixAP;
            set => SetProperty(ref _cervixAP, value);
        }

        private bool _shouldMeasureCervix = true;
        public bool ShouldMeasureCervix
        {
            get => _shouldMeasureCervix;
            set => SetProperty(ref _shouldMeasureCervix, value);
        }
        
        private string _myometriumEcho = string.Empty;
        public string MyometriumEcho 
        { 
            get => _myometriumEcho;
            set => SetProperty(ref _myometriumEcho, value);
        }

        private bool _myometriumThickeningFocal;
        public bool MyometriumThickeningFocal 
        { 
            get => _myometriumThickeningFocal;
            set => SetProperty(ref _myometriumThickeningFocal, value);
        }

        private bool _myometriumThickeningDiffuse;
        public bool MyometriumThickeningDiffuse 
        { 
            get => _myometriumThickeningDiffuse;
            set => SetProperty(ref _myometriumThickeningDiffuse, value);
        }

        private bool _myometriumThickeningNodule;
        public bool MyometriumThickeningNodule 
        { 
            get => _myometriumThickeningNodule;
            set => SetProperty(ref _myometriumThickeningNodule, value);
        }

        // 结节（动态添加）
        public ObservableCollection<MyometriumNodule> Nodules { get; } = new ObservableCollection<MyometriumNodule>();

        private MyometriumNodule? _selectedNodule;
        public MyometriumNodule? SelectedNodule
        {
            get => _selectedNodule;
            set => SetProperty(ref _selectedNodule, value);
        }

        /// <summary>
        /// 是否有多发结节（≥2），用于显示“报告方式”选项。
        /// </summary>
        public bool HasMultipleNodules => Nodules.Count >= 2;

        public void AddNodule()
        {
            var n = new MyometriumNodule();
            Nodules.Add(n);
            SelectedNodule = n;
            OnPropertyChanged(nameof(HasMultipleNodules));
        }

        /// <summary>
        /// 删除指定结节（用于每个结节卡片内的“删除此结节”按钮）。
        /// </summary>
        public void RemoveNodule(MyometriumNodule? nodule)
        {
            if (nodule == null || !Nodules.Contains(nodule)) return;

            var idx = Nodules.IndexOf(nodule);
            Nodules.Remove(nodule);

            if (Nodules.Count == 0)
            {
                SelectedNodule = null;
                return;
            }

            SelectedNodule = Nodules[Math.Max(0, Math.Min(idx, Nodules.Count - 1))];
            OnPropertyChanged(nameof(HasMultipleNodules));
        }

        public void RemoveSelectedNodule()
        {
            RemoveNodule(SelectedNodule ?? Nodules.LastOrDefault());
        }

        /// <summary>
        /// 多发时报告方式：true=只描述较大者（按体积自动取最大），false=全部写出。
        /// </summary>
        private bool _reportOnlyLargestNodule = true;
        public bool ReportOnlyLargestNodule
        {
            get => _reportOnlyLargestNodule;
            set => SetProperty(ref _reportOnlyLargestNodule, value);
        }

        // Nodule Details
        private string _noduleCount = string.Empty;
        public string NoduleCount 
        { 
            get => _noduleCount;
            set => SetProperty(ref _noduleCount, value);
        }

        // 移除NoduleType，不再使用

        // 较大结节（用于多发时只测量较大）
        private double _largestNoduleLength;
        public double LargestNoduleLength 
        { 
            get => _largestNoduleLength;
            set => SetProperty(ref _largestNoduleLength, value);
        }

        private double _largestNoduleWidth;
        public double LargestNoduleWidth 
        { 
            get => _largestNoduleWidth;
            set => SetProperty(ref _largestNoduleWidth, value);
        }

        private double _largestNoduleHeight;
        public double LargestNoduleHeight 
        { 
            get => _largestNoduleHeight;
            set => SetProperty(ref _largestNoduleHeight, value);
        }

        private string _largestNoduleLocation = string.Empty;
        public string LargestNoduleLocation 
        { 
            get => _largestNoduleLocation;
            set => SetProperty(ref _largestNoduleLocation, value);
        }

        private string _largestNoduleEcho = string.Empty;
        public string LargestNoduleEcho 
        { 
            get => _largestNoduleEcho;
            set => SetProperty(ref _largestNoduleEcho, value);
        }

        // 单发或第一个结节
        private double _noduleLength;
        public double NoduleLength 
        { 
            get => _noduleLength;
            set => SetProperty(ref _noduleLength, value);
        }

        private double _noduleWidth;
        public double NoduleWidth 
        { 
            get => _noduleWidth;
            set => SetProperty(ref _noduleWidth, value);
        }

        private double _noduleHeight;
        public double NoduleHeight 
        { 
            get => _noduleHeight;
            set => SetProperty(ref _noduleHeight, value);
        }

        private string _noduleLocation = string.Empty;
        public string NoduleLocation 
        { 
            get => _noduleLocation;
            set => SetProperty(ref _noduleLocation, value);
        }

        private string _noduleEcho = string.Empty;
        public string NoduleEcho 
        { 
            get => _noduleEcho;
            set => SetProperty(ref _noduleEcho, value);
        }

        // 第二个结节（用于多发时全部写出）
        private double _secondNoduleLength;
        public double SecondNoduleLength 
        { 
            get => _secondNoduleLength;
            set => SetProperty(ref _secondNoduleLength, value);
        }

        private double _secondNoduleWidth;
        public double SecondNoduleWidth 
        { 
            get => _secondNoduleWidth;
            set => SetProperty(ref _secondNoduleWidth, value);
        }

        private double _secondNoduleHeight;
        public double SecondNoduleHeight 
        { 
            get => _secondNoduleHeight;
            set => SetProperty(ref _secondNoduleHeight, value);
        }

        private string _secondNoduleLocation = string.Empty;
        public string SecondNoduleLocation 
        { 
            get => _secondNoduleLocation;
            set => SetProperty(ref _secondNoduleLocation, value);
        }

        private string _secondNoduleEcho = string.Empty;
        public string SecondNoduleEcho 
        { 
            get => _secondNoduleEcho;
            set => SetProperty(ref _secondNoduleEcho, value);
        }

        private string _noduleBoundary = string.Empty;
        public string NoduleBoundary 
        { 
            get => _noduleBoundary;
            set => SetProperty(ref _noduleBoundary, value);
        }

        private bool _noduleProtrudes;
        public bool NoduleProtrudes 
        { 
            get => _noduleProtrudes;
            set => SetProperty(ref _noduleProtrudes, value);
        }

        private bool _noduleCompressesCavity;
        public bool NoduleCompressesCavity 
        { 
            get => _noduleCompressesCavity;
            set => SetProperty(ref _noduleCompressesCavity, value);
        }

        private bool _measureOnlyLargest;
        public bool MeasureOnlyLargest 
        { 
            get => _measureOnlyLargest;
            set => SetProperty(ref _measureOnlyLargest, value);
        }

        private string _noduleSizeLocation = string.Empty; // Legacy, kept for compatibility if needed, but we will likely remove usage
        public string NoduleSizeLocation 
        { 
            get => _noduleSizeLocation;
            set => SetProperty(ref _noduleSizeLocation, value);
        }
    }

    public class MyometriumNodule : ObservableObject
    {
        private string _location = string.Empty;
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        private double _length;
        public double Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private double _height;
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        private string _echo = string.Empty;
        public string Echo
        {
            get => _echo;
            set => SetProperty(ref _echo, value);
        }

        private string _boundary = string.Empty;
        public string Boundary
        {
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }

        private bool _protrudes;
        public bool Protrudes
        {
            get => _protrudes;
            set => SetProperty(ref _protrudes, value);
        }

        private bool _compressesCavity;
        public bool CompressesCavity
        {
            get => _compressesCavity;
            set => SetProperty(ref _compressesCavity, value);
        }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Location) &&
            string.IsNullOrWhiteSpace(Echo) &&
            string.IsNullOrWhiteSpace(Boundary) &&
            Length == 0 && Width == 0 && Height == 0 &&
            !Protrudes && !CompressesCavity;
    }

    public class Endometrium : ObservableObject
    {
        private double _thickness;
        public double Thickness 
        { 
            get => _thickness;
            set => SetProperty(ref _thickness, value);
        }

        private bool _cannotMeasure;
        public bool CannotMeasure 
        { 
            get => _cannotMeasure;
            set
            {
                if (SetProperty(ref _cannotMeasure, value) && value)
                {
                    Thickness = 0;
                }
            }
        }

        private bool _isSingleLayer;
        public bool IsSingleLayer 
        { 
            get => _isSingleLayer;
            set => SetProperty(ref _isSingleLayer, value);
        }

        private bool _shouldMeasureFlow;
        public bool ShouldMeasureFlow 
        { 
            get => _shouldMeasureFlow;
            set => SetProperty(ref _shouldMeasureFlow, value);
        }

        private string _echoType = string.Empty;
        public string EchoType 
        { 
            get => _echoType;
            set => SetProperty(ref _echoType, value);
        }

        private bool _echoUniform;
        public bool EchoUniform 
        { 
            get => _echoUniform;
            set
            {
                if (SetProperty(ref _echoUniform, value) && value)
                {
                    if (EchoNonUniform) EchoNonUniform = false;
                    NonUniformNoCyst = false;
                    NonUniformRegularCyst = false;
                    NonUniformIrregularCyst = false;
                }
            }
        }

        private bool _echoNonUniform;
        public bool EchoNonUniform 
        { 
            get => _echoNonUniform;
            set
            {
                if (SetProperty(ref _echoNonUniform, value))
                {
                    if (value)
                    {
                        if (EchoUniform) EchoUniform = false;
                    }
                    else
                    {
                        NonUniformNoCyst = false;
                        NonUniformRegularCyst = false;
                        NonUniformIrregularCyst = false;
                    }
                }
            }
        }

        private bool _nonUniformNoCyst;
        public bool NonUniformNoCyst 
        { 
            get => _nonUniformNoCyst;
            set => SetProperty(ref _nonUniformNoCyst, value);
        }

        private bool _nonUniformRegularCyst;
        public bool NonUniformRegularCyst 
        { 
            get => _nonUniformRegularCyst;
            set => SetProperty(ref _nonUniformRegularCyst, value);
        }

        private bool _nonUniformIrregularCyst;
        public bool NonUniformIrregularCyst 
        { 
            get => _nonUniformIrregularCyst;
            set => SetProperty(ref _nonUniformIrregularCyst, value);
        }

        private string _midline = string.Empty;
        public string Midline 
        { 
            get => _midline;
            set => SetProperty(ref _midline, value);
        }

        private string _junctionalZone = string.Empty;
        public string JunctionalZone 
        { 
            get => _junctionalZone;
            set => SetProperty(ref _junctionalZone, value);
        }
        
        private bool _hasNoFlow;
        public bool HasNoFlow 
        { 
            get => _hasNoFlow;
            set
            {
                if (SetProperty(ref _hasNoFlow, value) && value)
                {
                    if (HasFlow) HasFlow = false;
                    FlowAmount = string.Empty;
                    FlowPattern = string.Empty;
                }
            }
        }

        private bool _hasFlow;
        public bool HasFlow 
        { 
            get => _hasFlow;
            set
            {
                if (SetProperty(ref _hasFlow, value))
                {
                    if (value)
                    {
                        if (HasNoFlow) HasNoFlow = false;
                    }
                    else
                    {
                        FlowAmount = string.Empty;
                        FlowPattern = string.Empty;
                    }
                }
            }
        }

        private string _flowAmount = string.Empty;
        public string FlowAmount 
        { 
            get => _flowAmount;
            set => SetProperty(ref _flowAmount, value);
        }

        private string _flowPattern = string.Empty;
        public string FlowPattern 
        { 
            get => _flowPattern;
            set => SetProperty(ref _flowPattern, value);
        }
    }

    public class UterineCavity : ObservableObject
    {
        private bool _hasLesion;
        public bool HasLesion 
        { 
            get => _hasLesion;
            set => SetProperty(ref _hasLesion, value);
        }

        private double _length;
        public double Length 
        { 
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private double _apDiameter;
        public double APDiameter 
        { 
            get => _apDiameter;
            set => SetProperty(ref _apDiameter, value);
        }

        private double _width;
        public double Width 
        { 
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private string _location = string.Empty;
        public string Location 
        { 
            get => _location;
            set => SetProperty(ref _location, value);
        }
        
        private bool _isPedunculated;
        public bool IsPedunculated 
        { 
            get => _isPedunculated;
            set => SetProperty(ref _isPedunculated, value);
        }

        private double _baseDiameter;
        public double BaseDiameter 
        { 
            get => _baseDiameter;
            set => SetProperty(ref _baseDiameter, value);
        }

        private double _maxTransverseDiameter;
        public double MaxTransverseDiameter 
        { 
            get => _maxTransverseDiameter;
            set => SetProperty(ref _maxTransverseDiameter, value);
        }
        
        private string _echoType = string.Empty;
        public string EchoType 
        { 
            get => _echoType;
            set => SetProperty(ref _echoType, value);
        }

        private string _echoUniformity = string.Empty;
        public string EchoUniformity 
        { 
            get => _echoUniformity;
            set => SetProperty(ref _echoUniformity, value);
        }

        private string _boundary = string.Empty;
        public string Boundary 
        { 
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }
        
        private bool _hasNoFlow;
        public bool HasNoFlow 
        { 
            get => _hasNoFlow;
            set
            {
                if (SetProperty(ref _hasNoFlow, value) && value)
                {
                    if (HasFlow) HasFlow = false;
                    FlowAmount = string.Empty;
                    FlowPattern = string.Empty;
                }
            }
        }

        private bool _hasFlow;
        public bool HasFlow 
        { 
            get => _hasFlow;
            set
            {
                if (SetProperty(ref _hasFlow, value))
                {
                    if (value)
                    {
                        if (HasNoFlow) HasNoFlow = false;
                    }
                    else
                    {
                        FlowAmount = string.Empty;
                        FlowPattern = string.Empty;
                    }
                }
            }
        }

        private string _flowAmount = string.Empty;
        public string FlowAmount 
        { 
            get => _flowAmount;
            set => SetProperty(ref _flowAmount, value);
        }

        private string _flowPattern = string.Empty;
        public string FlowPattern 
        { 
            get => _flowPattern;
            set => SetProperty(ref _flowPattern, value);
        }
    }

    public class Ovary : ObservableObject
    {
        public string Side { get; }

        public Ovary(string side)
        {
            Side = side;
        }

        private double _length;
        public double Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private double _height;
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        private int _cystCount;
        public int CystCount
        {
            get => _cystCount;
            set => SetProperty(ref _cystCount, value);
        }

        private double _maxCystDiameter;
        public double MaxCystDiameter
        {
            get => _maxCystDiameter;
            set => SetProperty(ref _maxCystDiameter, value);
        }
    }

    /// <summary>
    /// 卵巢/附件按部位分别描述的模型（一级：左卵巢/右卵巢/左附件/右附件；二级：正常/异常）。
    /// </summary>
    public class AdnexaRegion : ObservableObject
    {
        public AdnexaRegion(string name)
        {
            Name = name;
            // 默认选“正常”，避免一打开就是空状态
            Evaluation = "正常";
        }

        public string Name { get; }

        private string _evaluation = "正常";
        public string Evaluation
        {
            get => _evaluation;
            set => SetProperty(ref _evaluation, value);
        }

        public bool IsNormal
        {
            get => Evaluation == "正常";
            set
            {
                if (!value) return;
                Evaluation = "正常";
                OnPropertyChanged(nameof(IsNormal));
                OnPropertyChanged(nameof(IsAbnormal));
            }
        }

        public bool IsAbnormal
        {
            get => Evaluation == "异常";
            set
            {
                if (!value) return;
                Evaluation = "异常";
                OnPropertyChanged(nameof(IsNormal));
                OnPropertyChanged(nameof(IsAbnormal));
            }
        }

        // 正常时：大小 + 囊性回声（卵泡）数量/较大直径（也可用于附件区的简单描述）
        private double _length;
        public double Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private double _height;
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        private int _cystCount;
        public int CystCount
        {
            get => _cystCount;
            set => SetProperty(ref _cystCount, value);
        }

        private double _maxCystDiameter;
        public double MaxCystDiameter
        {
            get => _maxCystDiameter;
            set => SetProperty(ref _maxCystDiameter, value);
        }

        // 异常类目（多选）
        private bool _hasUnilocularCyst;
        public bool HasUnilocularCyst
        {
            get => _hasUnilocularCyst;
            set => SetProperty(ref _hasUnilocularCyst, value);
        }
        public UnilocularCyst UnilocularCyst { get; } = new UnilocularCyst();

        private bool _hasMultilocularCyst;
        public bool HasMultilocularCyst
        {
            get => _hasMultilocularCyst;
            set => SetProperty(ref _hasMultilocularCyst, value);
        }
        public MultilocularCyst MultilocularCyst { get; } = new MultilocularCyst();

        private bool _hasSolidCyst;
        public bool HasSolidCyst
        {
            get => _hasSolidCyst;
            set => SetProperty(ref _hasSolidCyst, value);
        }
        public SolidCyst SolidCyst { get; } = new SolidCyst();

        private bool _hasSolidMass;
        public bool HasSolidMass
        {
            get => _hasSolidMass;
            set => SetProperty(ref _hasSolidMass, value);
        }
        public SolidMass SolidMass { get; } = new SolidMass();

        /// <summary>
        /// 是否有任何异常类型被选中（用于状态指示器）
        /// </summary>
        public bool HasAnyAbnormality => HasUnilocularCyst || HasMultilocularCyst || HasSolidCyst || HasSolidMass;

        // 展开状态管理（用于UI中Expander的展开状态）
        private bool _isUnilocularExpanded;
        public bool IsUnilocularExpanded
        {
            get => _isUnilocularExpanded;
            set => SetProperty(ref _isUnilocularExpanded, value);
        }

        private bool _isMultilocularExpanded;
        public bool IsMultilocularExpanded
        {
            get => _isMultilocularExpanded;
            set => SetProperty(ref _isMultilocularExpanded, value);
        }

        private bool _isSolidCystExpanded;
        public bool IsSolidCystExpanded
        {
            get => _isSolidCystExpanded;
            set => SetProperty(ref _isSolidCystExpanded, value);
        }

        private bool _isSolidMassExpanded;
        public bool IsSolidMassExpanded
        {
            get => _isSolidMassExpanded;
            set => SetProperty(ref _isSolidMassExpanded, value);
        }
    }

    public abstract class LesionBase : ObservableObject
    {
        private double _length;
        public double Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private double _height;
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        private string _location = string.Empty;
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        protected string _boundary = string.Empty; // 规则/不规则
        public virtual string Boundary
        {
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }

        protected string _shadow = string.Empty; // 有/无
        public virtual string Shadow
        {
            get => _shadow;
            set => SetProperty(ref _shadow, value);
        }

        protected int _bloodFlowScore;
        public virtual int BloodFlowScore
        {
            get => _bloodFlowScore;
            set => SetProperty(ref _bloodFlowScore, value);
        }
    }

    public class UnilocularCyst : LesionBase
    {
        private bool _isSimpleCyst;
        public bool IsSimpleCyst 
        { 
            get => _isSimpleCyst;
            set => SetProperty(ref _isSimpleCyst, value);
        }

        private bool _isNonSimpleCyst;
        public bool IsNonSimpleCyst 
        { 
            get => _isNonSimpleCyst;
            set => SetProperty(ref _isNonSimpleCyst, value);
        }

        private bool _echoSmoothWall;
        public bool EchoSmoothWall 
        { 
            get => _echoSmoothWall;
            set => SetProperty(ref _echoSmoothWall, value);
        }

        private bool _echoRoughWall;
        public bool EchoRoughWall 
        { 
            get => _echoRoughWall;
            set => SetProperty(ref _echoRoughWall, value);
        }

        private bool _echoDenseDots;
        public bool EchoDenseDots 
        { 
            get => _echoDenseDots;
            set => SetProperty(ref _echoDenseDots, value);
        }

        private bool _echoFlocculent;
        public bool EchoFlocculent 
        { 
            get => _echoFlocculent;
            set => SetProperty(ref _echoFlocculent, value);
        }

        private bool _echoGrid;
        public bool EchoGrid 
        { 
            get => _echoGrid;
            set => SetProperty(ref _echoGrid, value);
        }

        private bool _echoStrongMass;
        public bool EchoStrongMass 
        { 
            get => _echoStrongMass;
            set => SetProperty(ref _echoStrongMass, value);
        }

        private bool _echoShortLines;
        public bool EchoShortLines 
        { 
            get => _echoShortLines;
            set => SetProperty(ref _echoShortLines, value);
        }

        private bool _echoWeakDots;
        public bool EchoWeakDots 
        { 
            get => _echoWeakDots;
            set => SetProperty(ref _echoWeakDots, value);
        }

        private bool _echoPatchy;
        public bool EchoPatchy 
        { 
            get => _echoPatchy;
            set => SetProperty(ref _echoPatchy, value);
        }

        private string _echoOther = string.Empty;
        public string EchoOther 
        { 
            get => _echoOther;
            set
            {
                if (SetProperty(ref _echoOther, value) && !string.IsNullOrWhiteSpace(value) && !HasEchoOther)
                {
                    HasEchoOther = true;
                }
            }
        }

        private bool _hasEchoOther;
        public bool HasEchoOther
        {
            get => _hasEchoOther;
            set
            {
                if (SetProperty(ref _hasEchoOther, value) && !value)
                {
                    EchoOther = string.Empty;
                }
            }
        }
        
        private string _location = string.Empty; // 位置（单选）
        public new string Location
        {
            get => _location;
            set
            {
                if (SetProperty(ref _location, value) && value != "其他")
                {
                    LocationOther = string.Empty;
                }
            }
        }

        private string _locationOther = string.Empty;
        public string LocationOther
        {
            get => _locationOther;
            set => SetProperty(ref _locationOther, value);
        }
        
        private new string _boundary = string.Empty; // 规则/不规则
        public new string Boundary
        {
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }
        
        private new string _shadow = string.Empty; // 有/无
        public new string Shadow
        {
            get => _shadow;
            set => SetProperty(ref _shadow, value);
        }
        
        private new int _bloodFlowScore;
        public new int BloodFlowScore
        {
            get => _bloodFlowScore;
            set => SetProperty(ref _bloodFlowScore, value);
        }
    }

    public class MultilocularCyst : LesionBase
    {
        private string _location = string.Empty;
        public new string Location
        {
            get => _location;
            set
            {
                if (SetProperty(ref _location, value) && value != "其他")
                {
                    LocationOther = string.Empty;
                }
            }
        }

        private string _locationOther = string.Empty;
        public string LocationOther
        {
            get => _locationOther;
            set => SetProperty(ref _locationOther, value);
        }
        
        // Echo properties
        private bool _echoSmoothWall;
        public bool EchoSmoothWall 
        { 
            get => _echoSmoothWall;
            set => SetProperty(ref _echoSmoothWall, value);
        }

        private bool _echoRoughWall;
        public bool EchoRoughWall 
        { 
            get => _echoRoughWall;
            set => SetProperty(ref _echoRoughWall, value);
        }

        private bool _echoSmoothSeptum;
        public bool EchoSmoothSeptum 
        { 
            get => _echoSmoothSeptum;
            set => SetProperty(ref _echoSmoothSeptum, value);
        }

        private bool _echoRoughSeptum;
        public bool EchoRoughSeptum 
        { 
            get => _echoRoughSeptum;
            set => SetProperty(ref _echoRoughSeptum, value);
        }

        private bool _echoGoodTransmission;
        public bool EchoGoodTransmission 
        { 
            get => _echoGoodTransmission;
            set => SetProperty(ref _echoGoodTransmission, value);
        }

        private bool _echoPoorTransmission;
        public bool EchoPoorTransmission 
        { 
            get => _echoPoorTransmission;
            set => SetProperty(ref _echoPoorTransmission, value);
        }

        private bool _echoDenseDots;
        public bool EchoDenseDots 
        { 
            get => _echoDenseDots;
            set => SetProperty(ref _echoDenseDots, value);
        }

        private bool _echoFlocculent;
        public bool EchoFlocculent 
        { 
            get => _echoFlocculent;
            set => SetProperty(ref _echoFlocculent, value);
        }

        private bool _echoStrongMass;
        public bool EchoStrongMass 
        { 
            get => _echoStrongMass;
            set => SetProperty(ref _echoStrongMass, value);
        }

        private bool _echoShortLines;
        public bool EchoShortLines 
        { 
            get => _echoShortLines;
            set => SetProperty(ref _echoShortLines, value);
        }

        private bool _echoWeakDots;
        public bool EchoWeakDots 
        { 
            get => _echoWeakDots;
            set => SetProperty(ref _echoWeakDots, value);
        }

        private bool _echoPatchy;
        public bool EchoPatchy 
        { 
            get => _echoPatchy;
            set => SetProperty(ref _echoPatchy, value);
        }

        private bool _echoRegularInnerWall;
        public bool EchoRegularInnerWall 
        { 
            get => _echoRegularInnerWall;
            set => SetProperty(ref _echoRegularInnerWall, value);
        }

        private bool _echoIrregularInnerWall;
        public bool EchoIrregularInnerWall 
        { 
            get => _echoIrregularInnerWall;
            set => SetProperty(ref _echoIrregularInnerWall, value);
        }

        private bool _echoMoreThan10Locules;
        public bool EchoMoreThan10Locules 
        { 
            get => _echoMoreThan10Locules;
            set => SetProperty(ref _echoMoreThan10Locules, value);
        }

        private string _echoOther = string.Empty;
        public string EchoOther 
        { 
            get => _echoOther;
            set
            {
                if (SetProperty(ref _echoOther, value) && !string.IsNullOrWhiteSpace(value) && !HasEchoOther)
                {
                    HasEchoOther = true;
                }
            }
        }

        private bool _hasEchoOther;
        public bool HasEchoOther
        {
            get => _hasEchoOther;
            set
            {
                if (SetProperty(ref _hasEchoOther, value) && !value)
                {
                    EchoOther = string.Empty;
                }
            }
        }

        private bool _flowOnSeptum;
        public bool FlowOnSeptum 
        { 
            get => _flowOnSeptum;
            set => SetProperty(ref _flowOnSeptum, value);
        }

        private bool _flowOnWall;
        public bool FlowOnWall 
        { 
            get => _flowOnWall;
            set => SetProperty(ref _flowOnWall, value);
        }
        
        private new string _boundary = string.Empty;
        public new string Boundary
        {
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }
        
        private new string _shadow = string.Empty;
        public new string Shadow
        {
            get => _shadow;
            set => SetProperty(ref _shadow, value);
        }
        
        private new int _bloodFlowScore;
        public new int BloodFlowScore
        {
            get => _bloodFlowScore;
            set => SetProperty(ref _bloodFlowScore, value);
        }
    }

    public class SolidCyst : LesionBase
    {
        // Cystic Part Echo
        private bool _echoSmoothWall;
        public bool EchoSmoothWall 
        { 
            get => _echoSmoothWall;
            set => SetProperty(ref _echoSmoothWall, value);
        }

        private bool _echoRoughWall;
        public bool EchoRoughWall 
        { 
            get => _echoRoughWall;
            set => SetProperty(ref _echoRoughWall, value);
        }

        private bool _echoSmoothSeptum;
        public bool EchoSmoothSeptum 
        { 
            get => _echoSmoothSeptum;
            set => SetProperty(ref _echoSmoothSeptum, value);
        }

        private bool _echoRoughSeptum;
        public bool EchoRoughSeptum 
        { 
            get => _echoRoughSeptum;
            set => SetProperty(ref _echoRoughSeptum, value);
        }

        private bool _echoGoodTransmission;
        public bool EchoGoodTransmission 
        { 
            get => _echoGoodTransmission;
            set => SetProperty(ref _echoGoodTransmission, value);
        }

        private bool _echoPoorTransmission;
        public bool EchoPoorTransmission 
        { 
            get => _echoPoorTransmission;
            set => SetProperty(ref _echoPoorTransmission, value);
        }

        private bool _echoDenseDots;
        public bool EchoDenseDots 
        { 
            get => _echoDenseDots;
            set => SetProperty(ref _echoDenseDots, value);
        }

        private bool _echoFlocculent;
        public bool EchoFlocculent 
        { 
            get => _echoFlocculent;
            set => SetProperty(ref _echoFlocculent, value);
        }

        private bool _echoGrid;
        public bool EchoGrid 
        { 
            get => _echoGrid;
            set => SetProperty(ref _echoGrid, value);
        }

        private bool _echoStrongMass;
        public bool EchoStrongMass 
        { 
            get => _echoStrongMass;
            set => SetProperty(ref _echoStrongMass, value);
        }

        private bool _echoShortLines;
        public bool EchoShortLines 
        { 
            get => _echoShortLines;
            set => SetProperty(ref _echoShortLines, value);
        }

        private bool _echoWeakDots;
        public bool EchoWeakDots 
        { 
            get => _echoWeakDots;
            set => SetProperty(ref _echoWeakDots, value);
        }

        private bool _echoPatchy;
        public bool EchoPatchy 
        { 
            get => _echoPatchy;
            set => SetProperty(ref _echoPatchy, value);
        }

        private bool _echoMoreThan10Locules;
        public bool EchoMoreThan10Locules 
        { 
            get => _echoMoreThan10Locules;
            set => SetProperty(ref _echoMoreThan10Locules, value);
        }

        private string _echoOther = string.Empty;
        public string EchoOther 
        { 
            get => _echoOther;
            set
            {
                if (SetProperty(ref _echoOther, value) && !string.IsNullOrWhiteSpace(value) && !HasEchoOther)
                {
                    HasEchoOther = true;
                }
            }
        }

        private bool _hasEchoOther;
        public bool HasEchoOther
        {
            get => _hasEchoOther;
            set
            {
                if (SetProperty(ref _hasEchoOther, value) && !value)
                {
                    EchoOther = string.Empty;
                }
            }
        }

        // Papillary
        private bool _hasNoPapillary;
        public bool HasNoPapillary
        {
            get => _hasNoPapillary;
            set => SetProperty(ref _hasNoPapillary, value);
        }
        
        private bool _hasPapillary;
        public bool HasPapillary
        {
            get => _hasPapillary;
            set => SetProperty(ref _hasPapillary, value);
        }
        
        private double _papillaryLength;
        public double PapillaryLength 
        { 
            get => _papillaryLength;
            set => SetProperty(ref _papillaryLength, value);
        }

        private double _papillaryWidth;
        public double PapillaryWidth 
        { 
            get => _papillaryWidth;
            set => SetProperty(ref _papillaryWidth, value);
        }

        private double _papillaryHeight;
        public double PapillaryHeight 
        { 
            get => _papillaryHeight;
            set => SetProperty(ref _papillaryHeight, value);
        }

        private double _papillaryHeightVal;
        public double PapillaryHeightVal 
        { 
            get => _papillaryHeightVal;
            set => SetProperty(ref _papillaryHeightVal, value);
        }
        
        // Papillary Echo (Multi-select)
        private bool _papillaryEchoLow;
        public bool PapillaryEchoLow 
        { 
            get => _papillaryEchoLow;
            set => SetProperty(ref _papillaryEchoLow, value);
        }

        private bool _papillaryEchoIso;
        public bool PapillaryEchoIso 
        { 
            get => _papillaryEchoIso;
            set => SetProperty(ref _papillaryEchoIso, value);
        }

        private bool _papillaryEchoHigh;
        public bool PapillaryEchoHigh 
        { 
            get => _papillaryEchoHigh;
            set => SetProperty(ref _papillaryEchoHigh, value);
        }

        private string _papillaryCount = string.Empty;
        public string PapillaryCount 
        { 
            get => _papillaryCount;
            set => SetProperty(ref _papillaryCount, value);
        }

        private string _papillaryContour = string.Empty;
        public string PapillaryContour 
        { 
            get => _papillaryContour;
            set => SetProperty(ref _papillaryContour, value);
        }

        private string _papillaryShadow = string.Empty;
        public string PapillaryShadow 
        { 
            get => _papillaryShadow;
            set => SetProperty(ref _papillaryShadow, value);
        }

        private bool _papillaryHasNoFlow;
        public bool PapillaryHasNoFlow 
        { 
            get => _papillaryHasNoFlow;
            set => SetProperty(ref _papillaryHasNoFlow, value);
        }

        private bool _papillaryHasFlow;
        public bool PapillaryHasFlow 
        { 
            get => _papillaryHasFlow;
            set => SetProperty(ref _papillaryHasFlow, value);
        }

        private string _papillaryFlowAmount = string.Empty;
        public string PapillaryFlowAmount 
        { 
            get => _papillaryFlowAmount;
            set => SetProperty(ref _papillaryFlowAmount, value);
        }

        // Solid Component (Non-Papillary)
        private double _solidLength;
        public double SolidLength 
        { 
            get => _solidLength;
            set => SetProperty(ref _solidLength, value);
        }

        private double _solidWidth;
        public double SolidWidth 
        { 
            get => _solidWidth;
            set => SetProperty(ref _solidWidth, value);
        }

        private double _solidHeight;
        public double SolidHeight 
        { 
            get => _solidHeight;
            set => SetProperty(ref _solidHeight, value);
        }
        
        // Solid Echo (Multi-select)
        private bool _solidEchoLow;
        public bool SolidEchoLow 
        { 
            get => _solidEchoLow;
            set => SetProperty(ref _solidEchoLow, value);
        }

        private bool _solidEchoIso;
        public bool SolidEchoIso 
        { 
            get => _solidEchoIso;
            set => SetProperty(ref _solidEchoIso, value);
        }

        private bool _solidEchoHigh;
        public bool SolidEchoHigh 
        { 
            get => _solidEchoHigh;
            set => SetProperty(ref _solidEchoHigh, value);
        }

        private bool _solidEchoOther;
        public bool SolidEchoOther 
        { 
            get => _solidEchoOther;
            set => SetProperty(ref _solidEchoOther, value);
        }

        private string _solidEchoOtherText = string.Empty;
        public string SolidEchoOtherText 
        { 
            get => _solidEchoOtherText;
            set => SetProperty(ref _solidEchoOtherText, value);
        }

        private string _solidBoundary = string.Empty;
        public string SolidBoundary 
        { 
            get => _solidBoundary;
            set => SetProperty(ref _solidBoundary, value);
        }

        private string _solidShadow = string.Empty;
        public string SolidShadow 
        { 
            get => _solidShadow;
            set => SetProperty(ref _solidShadow, value);
        }

        private bool _solidHasNoFlow;
        public bool SolidHasNoFlow 
        { 
            get => _solidHasNoFlow;
            set => SetProperty(ref _solidHasNoFlow, value);
        }

        private bool _solidHasFlow;
        public bool SolidHasFlow 
        { 
            get => _solidHasFlow;
            set => SetProperty(ref _solidHasFlow, value);
        }

        private string _solidFlowAmount = string.Empty;
        public string SolidFlowAmount 
        { 
            get => _solidFlowAmount;
            set => SetProperty(ref _solidFlowAmount, value);
        }
        
        private string _location = string.Empty;
        public new string Location
        {
            get => _location;
            set
            {
                if (SetProperty(ref _location, value) && value != "其他")
                {
                    LocationOther = string.Empty;
                }
            }
        }

        private string _locationOther = string.Empty;
        public string LocationOther
        {
            get => _locationOther;
            set => SetProperty(ref _locationOther, value);
        }
        
        private new string _boundary = string.Empty;
        public new string Boundary
        {
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }
        
        private new int _bloodFlowScore;
        public new int BloodFlowScore
        {
            get => _bloodFlowScore;
            set => SetProperty(ref _bloodFlowScore, value);
        }
    }

    public class SolidMass : LesionBase
    {
        private string _echoUniformity = string.Empty; // 均匀/不均匀
        public string EchoUniformity
        {
            get => _echoUniformity;
            set => SetProperty(ref _echoUniformity, value);
        }

        private string _echoType = string.Empty; // 低回声, 等回声, 高回声
        public string EchoType
        {
            get => _echoType;
            set => SetProperty(ref _echoType, value);
        }

        private string _locationOther = string.Empty;
        public string LocationOther
        {
            get => _locationOther;
            set => SetProperty(ref _locationOther, value);
        }
    }

    public class FluidLocation : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private double _depth;
        public double Depth
        {
            get => _depth;
            set => SetProperty(ref _depth, value);
        }
        
        private bool _isSelected;
        public bool IsSelected
        {
             get => _isSelected;
             set => SetProperty(ref _isSelected, value);
        }
    }
}
