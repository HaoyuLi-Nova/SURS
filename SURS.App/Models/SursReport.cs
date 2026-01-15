using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace SURS.App.Models
{
    public class SursReport : ObservableObject
    {
        // Image Paths
        public ObservableCollection<string> ImagePaths { get; } = new ObservableCollection<string>();

        // Patient Info & Header
        public string HospitalName { get; set; } = "首都医科大学附属北京妇产医院";
        public string HospitalBranch { get; set; } = string.Empty; // East/West
        public string LastMenstrualPeriod { get; set; } = string.Empty;

        // Uterus (Form 1)
        public Uterus Uterus { get; } = new Uterus();
        public Endometrium Endometrium { get; } = new Endometrium();
        public UterineCavity Cavity { get; } = new UterineCavity();

        // 卵巢正常/异常
        private bool _isNormal;
        public bool IsNormal
        {
            get => _isNormal;
            set => SetProperty(ref _isNormal, value);
        }

        public Ovary LeftOvary { get; } = new Ovary("Left");
        public Ovary RightOvary { get; } = new Ovary("Right");

        // Findings Categories (Checkboxes in the form)
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
        private bool _hasFluid;
        public bool HasFluid
        {
            get => _hasFluid;
            set => SetProperty(ref _hasFluid, value);
        }
        public ObservableCollection<FluidLocation> FluidLocations { get; } = new ObservableCollection<FluidLocation>();

        // Conclusion
        private string _oRadsScore = string.Empty;
        public string ORadsScore
        {
            get => _oRadsScore;
            set => SetProperty(ref _oRadsScore, value);
        }
        
        // Form 1 Conclusion
        public string UterusDescription { get; set; } = string.Empty;
        public string EndometriumDiagnosis { get; set; } = string.Empty; // Hyperplasia, Polyp, Cancer, Submucosal Myoma, Other
        
        // Footer
        public string Remarks { get; set; } = string.Empty;
        public string Typist { get; set; } = string.Empty;
        public string Diagnostician { get; set; } = string.Empty;
        public System.DateTime ReportDate { get; set; } = System.DateTime.Now;
    }

    public class Uterus : ObservableObject
    {
        public string Position { get; set; } = string.Empty; // Anteverted, Retroverted, Mid
        public double Length { get; set; }
        public double APDiameter { get; set; }
        public double Width { get; set; }
        public double CervixLength { get; set; }
        public double CervixAP { get; set; }
        
        public string MyometriumEcho { get; set; } = string.Empty; // Uniform, Non-uniform
        public string MyometriumThickening { get; set; } = string.Empty; // Focal, Diffuse
        public string NoduleSizeLocation { get; set; } = string.Empty;
    }

    public class Endometrium : ObservableObject
    {
        public double Thickness { get; set; }
        public bool CannotMeasure { get; set; }
        public string EchoType { get; set; } = string.Empty; // Low, Iso, High
        public string EchoUniformity { get; set; } = string.Empty; // Uniform, Non-uniform
        public string CysticArea { get; set; } = string.Empty; // None, Regular, Irregular
        public string Midline { get; set; } = string.Empty; // Linear, Non-linear, Irregular, Not Visible
        public string JunctionalZone { get; set; } = string.Empty; // Regular, Irregular, Interrupted, Not Visible
        
        public bool HasFlow { get; set; }
        public string FlowAmount { get; set; } = string.Empty; // Scanty, Moderate, Abundant
        public string FlowPattern { get; set; } = string.Empty; 
    }

    public class UterineCavity : ObservableObject
    {
        public bool HasLesion { get; set; }
        public double Length { get; set; }
        public double APDiameter { get; set; }
        public double Width { get; set; }
        public string Location { get; set; } = string.Empty;
        
        public bool IsPedunculated { get; set; } // Calculated from Base/Max ratio
        public double BaseDiameter { get; set; }
        public double MaxTransverseDiameter { get; set; }
        
        public string EchoType { get; set; } = string.Empty;
        public string EchoUniformity { get; set; } = string.Empty;
        public string Boundary { get; set; } = string.Empty;
        
        public bool HasFlow { get; set; }
        public string FlowAmount { get; set; } = string.Empty;
        public string FlowPattern { get; set; } = string.Empty;
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

        private string _boundary = string.Empty; // 规则/不规则
        public string Boundary
        {
            get => _boundary;
            set => SetProperty(ref _boundary, value);
        }

        private string _shadow = string.Empty; // 有/无
        public string Shadow
        {
            get => _shadow;
            set => SetProperty(ref _shadow, value);
        }

        private int _bloodFlowScore;
        public int BloodFlowScore
        {
            get => _bloodFlowScore;
            set => SetProperty(ref _bloodFlowScore, value);
        }
    }

    public class UnilocularCyst : LesionBase
    {
        private bool _isSimple;
        public bool IsSimple
        {
            get => _isSimple;
            set => SetProperty(ref _isSimple, value);
        }

        // Echo properties
        public bool EchoSmoothWall { get; set; }
        public bool EchoRoughWall { get; set; }
        public bool EchoDenseDots { get; set; }
        public bool EchoFlocculent { get; set; }
        public bool EchoGrid { get; set; }
        public bool EchoStrongMass { get; set; }
        public bool EchoShortLines { get; set; }
        public bool EchoWeakDots { get; set; }
        public bool EchoPatchy { get; set; }
        public string EchoOther { get; set; } = string.Empty;
    }

    public class MultilocularCyst : LesionBase
    {
        // Echo properties
        public bool EchoSmoothWall { get; set; }
        public bool EchoRoughWall { get; set; }
        public bool EchoSmoothSeptum { get; set; }
        public bool EchoRoughSeptum { get; set; }
        public bool EchoGoodTransmission { get; set; }
        public bool EchoPoorTransmission { get; set; }
        public bool EchoDenseDots { get; set; }
        public bool EchoFlocculent { get; set; }
        public bool EchoStrongMass { get; set; }
        public bool EchoShortLines { get; set; }
        public bool EchoWeakDots { get; set; }
        public bool EchoPatchy { get; set; }
        public bool EchoRegularInnerWall { get; set; }
        public bool EchoIrregularInnerWall { get; set; }
        public bool EchoMoreThan10Locules { get; set; }
        public string EchoOther { get; set; } = string.Empty;

        public bool FlowOnSeptum { get; set; }
        public bool FlowOnWall { get; set; }
    }

    public class SolidCyst : LesionBase
    {
        // Cystic Part Echo
        public bool EchoSmoothWall { get; set; }
        public bool EchoRoughWall { get; set; }
        public bool EchoSmoothSeptum { get; set; }
        public bool EchoRoughSeptum { get; set; }
        public bool EchoGoodTransmission { get; set; }
        public bool EchoPoorTransmission { get; set; }
        public bool EchoDenseDots { get; set; }
        public bool EchoFlocculent { get; set; }
        public bool EchoGrid { get; set; }
        public bool EchoStrongMass { get; set; }
        public bool EchoShortLines { get; set; }
        public bool EchoWeakDots { get; set; }
        public bool EchoPatchy { get; set; }
        public bool EchoMoreThan10Locules { get; set; }
        public string EchoOther { get; set; } = string.Empty;

        // Papillary
        private bool _hasPapillary;
        public bool HasPapillary
        {
            get => _hasPapillary;
            set => SetProperty(ref _hasPapillary, value);
        }
        
        public double PapillaryLength { get; set; }
        public double PapillaryWidth { get; set; }
        public double PapillaryHeight { get; set; }
        public double PapillaryHeightVal { get; set; } // "Height" in cm

        public string PapillaryEcho { get; set; } = string.Empty; // Low, Iso, High
        public string PapillaryCount { get; set; } = string.Empty; // 1, 2, 3, >3
        public string PapillaryContour { get; set; } = string.Empty; // Regular, Irregular
        public string PapillaryShadow { get; set; } = string.Empty;
        public bool PapillaryHasFlow { get; set; }
        public string PapillaryFlowAmount { get; set; } = string.Empty; // Scanty, Moderate, Abundant

        // Solid Component (Non-Papillary)
        public double SolidLength { get; set; }
        public double SolidWidth { get; set; }
        public double SolidHeight { get; set; }
        public string SolidEcho { get; set; } = string.Empty;
        public string SolidBoundary { get; set; } = string.Empty;
        public string SolidShadow { get; set; } = string.Empty;
        public bool SolidHasFlow { get; set; }
        public string SolidFlowAmount { get; set; } = string.Empty;
    }

    public class SolidMass : LesionBase
    {
        private string _echoUniformity = string.Empty; // Uniform/Non-uniform
        public string EchoUniformity
        {
            get => _echoUniformity;
            set => SetProperty(ref _echoUniformity, value);
        }

        private string _echoType = string.Empty; // Low, Iso, High
        public string EchoType
        {
            get => _echoType;
            set => SetProperty(ref _echoType, value);
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
