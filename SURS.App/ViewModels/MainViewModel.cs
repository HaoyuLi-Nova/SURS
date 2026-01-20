using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SURS.App.Models;
using SURS.App.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using System.IO;

namespace SURS.App.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly PdfService _pdfService;
        private System.Threading.CancellationTokenSource? _previewUpdateCancellation;
        private const int PreviewUpdateDelayMs = 200; // 增加防抖延迟到200ms，平衡性能与响应

        private SursReport _report = null!;
        public SursReport Report
        {
            get => _report;
            private set
            {
                if (_report != null)
                {
                    _report.PropertyChanged -= Report_PropertyChanged;
                }
                
                SetProperty(ref _report, value);
                
                if (_report != null)
                {
                    _report.PropertyChanged += Report_PropertyChanged;
                    SubscribeToNestedObjects(_report);
                    TriggerPreviewUpdate();
                }
            }
        }

        private BitmapImage? _previewImage;
        public BitmapImage? PreviewImage
        {
            get => _previewImage;
            private set => SetProperty(ref _previewImage, value);
        }

        private bool _isUpdatingPreview;
        public bool IsUpdatingPreview
        {
            get => _isUpdatingPreview;
            private set => SetProperty(ref _isUpdatingPreview, value);
        }

        private bool _isPreviewVisible = true;
        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            set => SetProperty(ref _isPreviewVisible, value);
        }

        // 基准缩放值：0.25 对应 100% 显示
        private const double BaseZoom = 0.25;
        
        private double _previewZoom = BaseZoom; // 初始值设为基准值（100%）
        public double PreviewZoom
        {
            get => _previewZoom;
            set
            {
                if (SetProperty(ref _previewZoom, Math.Max(BaseZoom, Math.Min(3.0, value))))
                {
                    // 当缩放值变化时，通知PreviewZoomPercent也更新
                    OnPropertyChanged(nameof(PreviewZoomPercent));
                }
            }
        }

        /// <summary>
        /// 获取相对于基准的缩放百分比（用于显示）
        /// </summary>
        public double PreviewZoomPercent
        {
            get => (PreviewZoom / BaseZoom) * 100; // 0.25 = 100%, 0.5 = 200%, 1.0 = 400%
        }

        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand SelectImageCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand TogglePreviewCommand { get; }
        public RelayCommand ZoomInPreviewCommand { get; }
        public RelayCommand ZoomOutPreviewCommand { get; }
        public RelayCommand ResetZoomPreviewCommand { get; }

        /// <summary>
        /// 处理鼠标滚轮缩放
        /// </summary>
        public bool HandleMouseWheelZoom(int delta, bool ctrlPressed = false)
        {
            // 仅当按住 Ctrl 时才缩放
            if (ctrlPressed)
            {
                // 基于基准值的10%作为步进（相对于显示100%的10%）
                double zoomStep = BaseZoom * 0.1; // 每次缩放10%（相对于基准值）
                if (delta > 0)
                {
                    PreviewZoom = Math.Min(3.0, PreviewZoom + zoomStep);
                }
                else
                {
                    PreviewZoom = Math.Max(BaseZoom, PreviewZoom - zoomStep);
                }
                return true; // 已处理缩放
            }
            return false; // 未处理，允许默认滚动
        }

        public MainViewModel()
        {
            _pdfService = new PdfService();
            ResetForm();

            ExportPdfCommand = new RelayCommand(ExportPdf);
            SelectImageCommand = new RelayCommand(SelectImage);
            ResetCommand = new RelayCommand(ResetForm);
            TogglePreviewCommand = new RelayCommand(TogglePreview);
            ZoomInPreviewCommand = new RelayCommand(() => PreviewZoom += BaseZoom * 0.5); // 每次增加50%（相对于基准）
            ZoomOutPreviewCommand = new RelayCommand(() => PreviewZoom -= BaseZoom * 0.5); // 每次减少50%（相对于基准）
            ResetZoomPreviewCommand = new RelayCommand(() => PreviewZoom = BaseZoom); // 重置到基准值（100%）
        }

        private void Report_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当报告属性变化时，立即触发预览更新（实时刷新）
            TriggerPreviewUpdate();
        }

        private void TriggerPreviewUpdate()
        {
            // 取消之前的更新任务
            _previewUpdateCancellation?.Cancel();
            _previewUpdateCancellation = new System.Threading.CancellationTokenSource();
            var token = _previewUpdateCancellation.Token;

            // 启动异步任务进行防抖和更新
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(PreviewUpdateDelayMs, token);
                    if (!token.IsCancellationRequested)
                    {
                        await UpdatePreviewAsync(token);
                    }
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    // 忽略取消
                }
                catch (Exception)
                {
                    // 忽略其他错误，确保不崩溃
                }
            });
        }

        private async System.Threading.Tasks.Task UpdatePreviewAsync(System.Threading.CancellationToken token)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() => IsUpdatingPreview = true);
                
                // 在后台线程生成预览
                var imageBytes = await System.Threading.Tasks.Task.Run(() => 
                {
                    if (token.IsCancellationRequested) return null;
                    return _pdfService.GeneratePreviewImage(Report);
                }, token);

                if (token.IsCancellationRequested) return;

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    // 在UI线程更新图片
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(imageBytes);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze(); 
                        
                        PreviewImage = bitmap;
                    });
                }
            }
            catch (Exception)
            {
                // 静默失败，不输出调试信息
            }
            finally
            {
                // 只有当任务未被取消时，才重置IsUpdatingPreview
                // 如果任务被取消，新的任务会接管IsUpdatingPreview的状态
                if (!token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() => IsUpdatingPreview = false);
                }
            }
        }

        private void SubscribeToNestedObjects(SursReport report)
        {
            // 订阅所有嵌套对象的属性变化
            if (report.Uterus != null)
                report.Uterus.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.Endometrium != null)
                report.Endometrium.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.Cavity != null)
                report.Cavity.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.LeftOvary != null)
                report.LeftOvary.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.RightOvary != null)
                report.RightOvary.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.UnilocularCyst != null)
                report.UnilocularCyst.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.MultilocularCyst != null)
                report.MultilocularCyst.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.SolidCyst != null)
                report.SolidCyst.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            if (report.SolidMass != null)
                report.SolidMass.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            
            // 监听集合变化
            report.ImagePaths.CollectionChanged += (s, e) => TriggerPreviewUpdate();
            report.FluidLocations.CollectionChanged += (s, e) => 
            {
                TriggerPreviewUpdate();
                // 当集合变化时，重新订阅新添加项的属性变化
                if (e.NewItems != null)
                {
                    foreach (FluidLocation fluid in e.NewItems)
                    {
                        fluid.PropertyChanged += (s2, e2) => TriggerPreviewUpdate();
                    }
                }
            };
            
            // 监听现有FluidLocation对象的属性变化
            foreach (var fluid in report.FluidLocations)
            {
                fluid.PropertyChanged += (s, e) => TriggerPreviewUpdate();
            }
        }

        private void ResetForm()
        {
            Report = new SursReport();
            InitializeData();
        }

        private void InitializeData()
        {
            // Initialize Fluid Locations
            Report.FluidLocations.Add(new FluidLocation { Name = "子宫后方" });
            Report.FluidLocations.Add(new FluidLocation { Name = "子宫前方" });
            Report.FluidLocations.Add(new FluidLocation { Name = "左附件区" });
            Report.FluidLocations.Add(new FluidLocation { Name = "右附件区" });
            Report.FluidLocations.Add(new FluidLocation { Name = "左髂窝" });
            Report.FluidLocations.Add(new FluidLocation { Name = "右髂窝" });
            Report.FluidLocations.Add(new FluidLocation { Name = "肝肾间隙" });
            Report.FluidLocations.Add(new FluidLocation { Name = "脾肾间隙" });
        }

        private void SelectImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (Report.ImagePaths.Count < 3)
                    {
                        Report.ImagePaths.Add(file);
                    }
                }
            }
        }

        private void TogglePreview()
        {
            IsPreviewVisible = !IsPreviewVisible;
        }

        private void ExportPdf()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"SURS_Report_{DateTime.Now:yyyyMMddHHmm}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                _pdfService.GeneratePdf(Report, dialog.FileName);
                MessageBox.Show("PDF 导出成功!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
