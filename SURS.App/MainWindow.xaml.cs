using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using SURS.App.ViewModels;

namespace SURS.App
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private Point _lastMousePosition;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Ignore if it's a multi-line TextBox that accepts return
                if (e.OriginalSource is TextBox textBox && textBox.AcceptsReturn)
                {
                    return;
                }
                
                // Ignore if modifier keys are pressed (e.g. Ctrl+Enter)
                if (Keyboard.Modifiers != ModifierKeys.None)
                {
                    return;
                }

                // Move focus to next element
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                if (Keyboard.FocusedElement is UIElement elementWithFocus)
                {
                    elementWithFocus.MoveFocus(request);
                    e.Handled = true;
                }
            }
        }

        private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel != null)
                {
                    // 处理缩放
                    viewModel.HandleMouseWheelZoom(e.Delta, Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
                    e.Handled = true;
                }
            }
        }

        private void PreviewScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && PreviewImageControl != null)
            {
                // 检查是否点击在图片上，并且图片已放大超过基准值（需要拖拽）
                var viewModel = DataContext as MainViewModel;
                const double baseZoom = 0.25; // 基准值（100%）
                if (viewModel != null && viewModel.PreviewZoom > baseZoom)
                {
                    var imagePosition = e.GetPosition(PreviewImageControl);
                    // 检查点击是否在图片范围内
                    if (imagePosition.X >= 0 && imagePosition.Y >= 0 && 
                        imagePosition.X <= PreviewImageControl.ActualWidth && 
                        imagePosition.Y <= PreviewImageControl.ActualHeight)
                    {
                        _isDragging = true;
                        _lastMousePosition = e.GetPosition(scrollViewer);
                        scrollViewer.CaptureMouse();
                        scrollViewer.Cursor = Cursors.Hand;
                        e.Handled = true;
                    }
                }
            }
        }

        private void PreviewScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                _isDragging = false;
                scrollViewer.ReleaseMouseCapture();
                scrollViewer.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void PreviewScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is ScrollViewer scrollViewer)
            {
                var currentPosition = e.GetPosition(scrollViewer);
                var offset = currentPosition - _lastMousePosition;

                // 更新滚动位置（反向移动，实现拖拽效果）
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - offset.X);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - offset.Y);

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
            else if (sender is ScrollViewer scrollViewer2 && PreviewImageControl != null)
            {
                // 更新鼠标光标：当缩放大于基准值时显示手型光标
                var viewModel = DataContext as MainViewModel;
                const double baseZoom = 0.25; // 基准值（100%）
                if (viewModel != null && viewModel.PreviewZoom > baseZoom)
                {
                    var imagePosition = e.GetPosition(PreviewImageControl);
                    if (imagePosition.X >= 0 && imagePosition.Y >= 0 && 
                        imagePosition.X <= PreviewImageControl.ActualWidth && 
                        imagePosition.Y <= PreviewImageControl.ActualHeight)
                    {
                        scrollViewer2.Cursor = Cursors.Hand;
                    }
                    else
                    {
                        scrollViewer2.Cursor = Cursors.Arrow;
                    }
                }
                else
                {
                    scrollViewer2.Cursor = Cursors.Arrow;
                }
            }
        }
    }
}
