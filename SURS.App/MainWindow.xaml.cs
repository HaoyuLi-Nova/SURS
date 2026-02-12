using System;
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

        // 预览面板相关的事件处理已移至 PreviewPanel 用户控件中

        protected override void OnClosed(EventArgs e)
            {
            // 释放 ViewModel 资源
            if (_boundViewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnClosed(e);
        }

        
    }
}
