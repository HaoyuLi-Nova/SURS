using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using SURS.App.ViewModels;
using System.ComponentModel;

namespace SURS.App
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private Point _lastMousePosition;
        private GridLength? _savedPreviewColumnWidth;
        private GridLength? _savedSplitterColumnWidth;
        private double? _savedPreviewMinWidth;
        private double? _savedPreviewMaxWidth;
        private MainViewModel? _boundViewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += MainWindow_DataContextChanged;
            DataContext = new MainViewModel();
            BindToViewModel(DataContext as MainViewModel);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保窗口在加载时最大化
            WindowState = WindowState.Maximized;
        }

        private void TogglePreview_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsPreviewVisible = !vm.IsPreviewVisible;
            }
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindToViewModel(e.NewValue as MainViewModel);
        }

        private void BindToViewModel(MainViewModel? viewModel)
        {
            if (_boundViewModel != null)
            {
                _boundViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _boundViewModel = viewModel;

            if (_boundViewModel != null)
            {
                _boundViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ApplyPreviewVisibility(_boundViewModel.IsPreviewVisible, isInitial: true);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsPreviewVisible))
            {
                if (sender is MainViewModel vm)
                {
                    ApplyPreviewVisibility(vm.IsPreviewVisible, isInitial: false);
                }
            }
        }

        private void ApplyPreviewVisibility(bool isVisible, bool isInitial)
        {
            if (PreviewColumn == null || SplitterColumn == null)
            {
                return;
            }

            if (isVisible)
            {
                if (_savedPreviewMinWidth.HasValue)
                    PreviewColumn.MinWidth = _savedPreviewMinWidth.Value;
                if (_savedPreviewMaxWidth.HasValue)
                    PreviewColumn.MaxWidth = _savedPreviewMaxWidth.Value;

                if (_savedPreviewColumnWidth.HasValue)
                {
                    PreviewColumn.Width = _savedPreviewColumnWidth.Value;
                }
                else if (isInitial)
                {
                    PreviewColumn.Width = new GridLength(0.75, GridUnitType.Star);
                }

                if (_savedSplitterColumnWidth.HasValue)
                {
                    SplitterColumn.Width = _savedSplitterColumnWidth.Value;
                }
                else if (isInitial)
                {
                    SplitterColumn.Width = new GridLength(5);
                }
            }
            else
            {
                _savedPreviewColumnWidth = PreviewColumn.Width;
                _savedSplitterColumnWidth = SplitterColumn.Width;
                _savedPreviewMinWidth = PreviewColumn.MinWidth;
                _savedPreviewMaxWidth = PreviewColumn.MaxWidth;

                PreviewColumn.MinWidth = 0;
                PreviewColumn.MaxWidth = 0;
                PreviewColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
            }
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
            // Remove unused 'scrollViewer' assignment
            if (sender is ScrollViewer)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    // 处理缩放
                    bool handled = viewModel.HandleMouseWheelZoom(e.Delta, Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
                    if (handled)
                    {
                        e.Handled = true;
                    }
                }
            }
        }

        private void PreviewScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && PreviewImageControl != null)
            {
                // 检查是否点击在图片上，并且图片已放大超过基准值（需要拖拽）
                const double baseZoom = 0.25; // 基准值（100%）
                if (DataContext is MainViewModel viewModel && viewModel.PreviewZoom > baseZoom)
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
                if (DataContext is MainViewModel viewModel && viewModel.PreviewZoom > baseZoom)
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
