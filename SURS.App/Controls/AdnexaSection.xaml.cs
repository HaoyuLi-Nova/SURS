using System.Windows.Controls;
using System.Windows.Input;

namespace SURS.App.Controls
{
    /// <summary>
    /// 卵巢与附件部分控件
    /// </summary>
    public partial class AdnexaSection : UserControl
    {
        public AdnexaSection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 处理评价 RadioButton 的点击事件，允许取消选择
        /// </summary>
        private void EvaluationRadioButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.IsChecked == true)
            {
                // 如果已经选中，再次点击时取消选择
                var dataContext = radioButton.DataContext;
                if (dataContext is SURS.App.Models.AdnexaRegion region)
                {
                    // 取消选择
                    if (radioButton.Content?.ToString() == "未见明显异常")
                    {
                        region.IsNormal = false;
                    }
                    else if (radioButton.Content?.ToString() == "见异常病灶")
                    {
                        region.IsAbnormal = false;
                    }
                    e.Handled = true;
                }
            }
        }
    }
}

