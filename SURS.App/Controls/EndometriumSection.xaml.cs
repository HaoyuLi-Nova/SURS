using System.Windows;
using System.Windows.Controls;
using SURS.App.ViewModels;

namespace SURS.App.Controls
{
    /// <summary>
    /// 子宫内膜部分控件：包含内膜厚度、回声、中线、结合带、CDFI、宫腔占位性病变
    /// </summary>
    public partial class EndometriumSection : UserControl
    {
        public EndometriumSection()
        {
            InitializeComponent();
        }

        // Clear actions are handled by re-click toggle behavior on RadioButtons.
    }
}

