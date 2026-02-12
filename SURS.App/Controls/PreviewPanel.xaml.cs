using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SURS.App.Controls
{
    /// <summary>
    /// 预览面板控件：包含 PDF 预览图片、缩放控制和拖拽功能
    /// </summary>
    public partial class PreviewPanel : UserControl
    {
        private bool _isDragging = false;
        private Point _lastMousePosition;

        public PreviewPanel()
        {
            InitializeComponent();
        }

        private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer && DataContext is ViewModels.MainViewModel viewModel)
            {
                // 处理缩放
                bool handled = viewModel.HandleMouseWheelZoom(e.Delta, Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
                if (handled)
                {
                    e.Handled = true;
                }
            }
        }

        private void PreviewScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && PreviewImageControl != null)
            {
                // 检查是否点击在图片上，并且图片已放大超过基准值（需要拖拽）
                const double baseZoom = 0.25; // 基准值（100%）
                if (DataContext is ViewModels.MainViewModel viewModel && viewModel.PreviewZoom > baseZoom)
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
                const double baseZoom = 0.25; // 基准值（100%）
                if (DataContext is ViewModels.MainViewModel viewModel && viewModel.PreviewZoom > baseZoom)
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

