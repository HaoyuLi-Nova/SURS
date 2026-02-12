using System.Windows;
using System.Windows.Controls;
using SURS.App.ViewModels;

namespace SURS.App.Controls
{
    /// <summary>
    /// 子宫部分控件：包含子宫位置、大小、宫颈、肌层回声、结节管理
    /// </summary>
    public partial class UterusSection : UserControl
    {
        public UterusSection()
        {
            InitializeComponent();
        }

        private void MyometriumEchoRadio_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is RadioButton radio && radio.IsChecked == true)
            {
                if (DataContext is MainViewModel vm && vm.Report?.Uterus != null)
                {
                    vm.Report.Uterus.MyometriumEcho = string.Empty;
                    vm.Report.Uterus.MyometriumThickeningFocal = false;
                    vm.Report.Uterus.MyometriumThickeningDiffuse = false;
                    vm.Report.Uterus.MyometriumThickeningNodule = false;
                }

                // Prevent the default behavior of re-checking
                e.Handled = true;
            }
        }

        // Clear handled by re-click behavior in MyometriumEchoRadio_PreviewMouseLeftButtonDown.
    }
}

