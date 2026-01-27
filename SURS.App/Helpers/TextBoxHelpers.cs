using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SURS.App.Helpers
{
    public static class TextBoxHelpers
    {
        public static readonly DependencyProperty IsNumericProperty =
            DependencyProperty.RegisterAttached("IsNumeric", typeof(bool), typeof(TextBoxHelpers), new PropertyMetadata(false, OnIsNumericChanged));

        public static readonly DependencyProperty NumericOnlyProperty =
            DependencyProperty.RegisterAttached("NumericOnly", typeof(bool), typeof(TextBoxHelpers), new PropertyMetadata(false, OnNumericOnlyChanged));

        public static bool GetIsNumeric(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsNumericProperty);
        }

        public static void SetIsNumeric(DependencyObject obj, bool value)
        {
            obj.SetValue(IsNumericProperty, value);
        }

        public static bool GetNumericOnly(DependencyObject obj)
        {
            return (bool)obj.GetValue(NumericOnlyProperty);
        }

        public static void SetNumericOnly(DependencyObject obj, bool value)
        {
            obj.SetValue(NumericOnlyProperty, value);
        }

        private static void OnNumericOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    textBox.PreviewTextInput += TextBox_PreviewTextInput;
                    DataObject.AddPastingHandler(textBox, TextBox_Pasting);
                }
                else
                {
                    textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                    DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
                }
            }
        }

        private static void OnIsNumericChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    textBox.PreviewTextInput += TextBox_PreviewTextInput;
                    DataObject.AddPastingHandler(textBox, TextBox_Pasting);
                    textBox.GotFocus += TextBox_GotFocus;
                    textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                    textBox.MouseLeftButtonDown += TextBox_MouseLeftButtonDown;
                    textBox.LostFocus += TextBox_LostFocus;
                }
                else
                {
                    textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                    DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
                    textBox.GotFocus -= TextBox_GotFocus;
                    textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                    textBox.MouseLeftButtonDown -= TextBox_MouseLeftButtonDown;
                    textBox.LostFocus -= TextBox_LostFocus;
                }
            }
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 获得焦点时：全选内容以便覆盖输入（参考现代UI/UX设计，如VS Code、Chrome等）
                // 延迟执行以确保在焦点事件之后，但要在用户输入之前
                textBox.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (textBox.IsFocused)
                    {
                        // 如果文本是"0"或空，自动选中全部文本
                        if (textBox.Text == "0" || string.IsNullOrWhiteSpace(textBox.Text))
                        {
                            textBox.SelectAll();
                        }
                        // 如果已经有文本且没有选择，则全选以便覆盖
                        else if (textBox.SelectionLength == 0)
                        {
                            textBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private static System.Collections.Generic.Dictionary<TextBox, System.DateTime> _lastClickTime = new System.Collections.Generic.Dictionary<TextBox, System.DateTime>();
        private const int DoubleClickInterval = 300; // 毫秒

        private static void TextBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var now = System.DateTime.Now;
                
                // 检查是否是双击
                if (_lastClickTime.ContainsKey(textBox))
                {
                    var timeSinceLastClick = (now - _lastClickTime[textBox]).TotalMilliseconds;
                    if (timeSinceLastClick < DoubleClickInterval)
                    {
                        // 双击：允许在特定位置插入（默认行为）
                        _lastClickTime.Remove(textBox);
                        return;
                    }
                }
                
                // 单击时：全选内容以便覆盖输入（参考现代UI/UX设计，如VS Code、Chrome等）
                _lastClickTime[textBox] = now;
                
                // 延迟执行以确保在鼠标事件之后
                textBox.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (textBox.IsFocused && e.ClickCount == 1)
                    {
                        // 如果还没有选择文本，则全选
                        if (textBox.SelectionLength == 0)
                        {
                            textBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && e.Key == Key.Enter)
            {
                // Enter键时，如果文本是"0"或空，清空文本以便直接输入
                if (textBox.Text == "0" || string.IsNullOrWhiteSpace(textBox.Text))
                {
                    textBox.Text = string.Empty;
                    textBox.SelectAll();
                }
                // 移动焦点到下一个控件
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                textBox.MoveFocus(request);
                e.Handled = true;
            }
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 失去焦点时清理点击时间记录
                if (_lastClickTime.ContainsKey(textBox))
                {
                    _lastClickTime.Remove(textBox);
                }
            }
        }

        private static bool IsTextAllowed(string text)
        {
            // Allow digits and one decimal point
            Regex regex = new Regex("[^0-9.]+"); 
            return !regex.IsMatch(text);
        }
    }
}
