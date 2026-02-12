using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SURS.App.Models;
using SURS.App.Services;
using SURS.App.Helpers;
using SURS.App.Common;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SURS.App.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _previewUpdateSemaphore = new SemaphoreSlim(1, 1);
        private volatile bool _previewUpdatePending;
        private readonly EventSubscriptionManager _subscriptionManager = new EventSubscriptionManager();

        private SursReport _report = null!;
        public SursReport Report
        {
            get => _report;
            private set
            {
                // 清理旧订阅
                _subscriptionManager.UnsubscribeAll();
                
                SetProperty(ref _report, value);
                
                if (_report != null)
                {
                    // 通过订阅管理器订阅 Report 的属性变化
                    _subscriptionManager.SubscribePropertyChanged(_report, Report_PropertyChanged);
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

        private double _previewZoom = PreviewConstants.BaseZoom; // 初始值设为基准值（100%）
        public double PreviewZoom
        {
            get => _previewZoom;
            set
            {
                if (SetProperty(ref _previewZoom, Math.Max(PreviewConstants.MinZoom, Math.Min(PreviewConstants.MaxZoom, value))))
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
            get => (PreviewZoom / PreviewConstants.BaseZoom) * 100; // 0.25 = 100%, 0.5 = 200%, 1.0 = 400%
        }

        private bool _isExporting;
        public bool IsExporting
        {
            get => _isExporting;
            private set => SetProperty(ref _isExporting, value);
        }

        public AsyncRelayCommand ExportPdfCommand { get; }
        public RelayCommand SelectImageCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand TogglePreviewCommand { get; }
        public RelayCommand ZoomInPreviewCommand { get; }
        public RelayCommand ZoomOutPreviewCommand { get; }
        public RelayCommand ResetZoomPreviewCommand { get; }
        public RelayCommand AddUterusNoduleCommand { get; }
        public RelayCommand<MyometriumNodule?> RemoveUterusNoduleCommand { get; }

        /// <summary>
        /// 处理鼠标滚轮缩放
        /// </summary>
        public bool HandleMouseWheelZoom(int delta, bool ctrlPressed = false)
        {
            // 仅当按住 Ctrl 时才缩放
            if (ctrlPressed)
            {
                if (delta > 0)
                {
                    PreviewZoom = Math.Min(PreviewConstants.MaxZoom, PreviewZoom + PreviewConstants.ZoomStep);
                }
                else
                {
                    PreviewZoom = Math.Max(PreviewConstants.MinZoom, PreviewZoom - PreviewConstants.ZoomStep);
                }
                return true; // 已处理缩放
            }
            return false; // 未处理，允许默认滚动
        }

        public MainViewModel()
        {
            _pdfService = new PdfService();
            _dialogService = new DialogService();
            _logger = new FileLogger();
            
            ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, () => !IsExporting);
            SelectImageCommand = new RelayCommand(SelectImage);
            ResetCommand = new RelayCommand(ResetForm);
            TogglePreviewCommand = new RelayCommand(TogglePreview);
            ZoomInPreviewCommand = new RelayCommand(() => PreviewZoom += PreviewConstants.BaseZoom * 0.5); // 每次增加50%（相对于基准）
            ZoomOutPreviewCommand = new RelayCommand(() => PreviewZoom -= PreviewConstants.BaseZoom * 0.5); // 每次减少50%（相对于基准）
            ResetZoomPreviewCommand = new RelayCommand(() => PreviewZoom = PreviewConstants.BaseZoom); // 重置到基准值（100%）

            RemoveUterusNoduleCommand = new RelayCommand<MyometriumNodule?>(
                nodule =>
                {
                    if (Report?.Uterus == null || nodule == null) return;
                    Report.Uterus.RemoveNodule(nodule);
                    RemoveUterusNoduleCommand?.NotifyCanExecuteChanged();
                    TriggerPreviewUpdate();
                },
                nodule => nodule != null && Report?.Uterus?.Nodules?.Contains(nodule) == true);

            AddUterusNoduleCommand = new RelayCommand(() =>
            {
                if (Report?.Uterus == null) return;
                Report.Uterus.AddNodule();
                RemoveUterusNoduleCommand?.NotifyCanExecuteChanged();
                TriggerPreviewUpdate();
            });

            ResetForm();
        }

        private void Report_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当报告属性变化时，立即触发预览更新（实时刷新）
            TriggerPreviewUpdate();
        }

        private void TriggerPreviewUpdate()
        {
            // 立即请求预览更新，不做节流或取消
            _previewUpdatePending = true;
            _ = ExecutePreviewUpdateAsync();
        }

        private async Task ExecutePreviewUpdateAsync()
        {
            if (!await _previewUpdateSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                while (_previewUpdatePending)
                {
                    _previewUpdatePending = false;
                    await UpdatePreviewAsync();
                }
            }
            finally
            {
                _previewUpdateSemaphore.Release();
            }
        }

        private async Task UpdatePreviewAsync()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() => IsUpdatingPreview = true);
                
                // 在后台线程生成预览
                var imageBytes = await System.Threading.Tasks.Task.Run(() => 
                {
                    return _pdfService.GeneratePreviewImage(Report, PreviewConstants.PreviewDpi);
                });

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    // 在UI线程更新图片
                    UpdatePreviewImage(imageBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("预览更新失败", ex);
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() => IsUpdatingPreview = false);
            }
        }

        /// <summary>
        /// 更新预览图片
        /// </summary>
        private void UpdatePreviewImage(byte[] imageBytes)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 释放旧图片资源
                var oldImage = PreviewImage;
                PreviewImage = null;
                oldImage?.StreamSource?.Dispose();
                
                // 创建新图片
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(imageBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); 
                
                PreviewImage = bitmap;
            });
        }

        private void SubscribeToNestedObjects(SursReport report)
        {
            // 订阅所有嵌套对象的属性变化
            if (report.Uterus != null)
                _subscriptionManager.SubscribePropertyChanged(report.Uterus, (s, e) => TriggerPreviewUpdate());

            if (report.Uterus != null)
            {
                // 监听子宫结节集合变化
                _subscriptionManager.SubscribeCollectionChanged(report.Uterus.Nodules, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    RemoveUterusNoduleCommand?.NotifyCanExecuteChanged();

                    if (e.NewItems != null)
                    {
                        foreach (MyometriumNodule n in e.NewItems)
                        {
                            _subscriptionManager.SubscribePropertyChanged(n, (s2, e2) => TriggerPreviewUpdate());
                        }
                    }
                });

                // 监听现有结节的属性变化
                foreach (var n in report.Uterus.Nodules)
                {
                    _subscriptionManager.SubscribePropertyChanged(n, (s, e) => TriggerPreviewUpdate());
                }
            }
            
            if (report.Endometrium != null)
                _subscriptionManager.SubscribePropertyChanged(report.Endometrium, (s, e) => TriggerPreviewUpdate());
            
            if (report.Cavity != null)
                _subscriptionManager.SubscribePropertyChanged(report.Cavity, (s, e) => TriggerPreviewUpdate());

            // 卵巢/附件：四个部位分别监听（左卵巢/右卵巢/左附件/右附件）
            void SubscribeRegion(AdnexaRegion region)
            {
                _subscriptionManager.SubscribePropertyChanged(region, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    // 当区域属性变化时，重新计算O-RADS分级
                    TriggerAutoORadsCalculation(report);
                });
                _subscriptionManager.SubscribePropertyChanged(region.UnilocularCyst, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    TriggerAutoORadsCalculation(report);
                });
                _subscriptionManager.SubscribePropertyChanged(region.MultilocularCyst, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    TriggerAutoORadsCalculation(report);
                });
                _subscriptionManager.SubscribePropertyChanged(region.SolidCyst, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    TriggerAutoORadsCalculation(report);
                });
                _subscriptionManager.SubscribePropertyChanged(region.SolidMass, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    TriggerAutoORadsCalculation(report);
                });
            }

            _subscriptionManager.SubscribeCollectionChanged(report.AdnexaRegions, (s, e) =>
            {
                TriggerPreviewUpdate();
                if (e.NewItems != null)
                {
                    foreach (AdnexaRegion r in e.NewItems)
                        SubscribeRegion(r);
                }
                // 当集合变化时，重新计算O-RADS分级
                TriggerAutoORadsCalculation(report);
            });

            foreach (var r in report.AdnexaRegions)
                SubscribeRegion(r);
            
            // 监听集合变化
            _subscriptionManager.SubscribeCollectionChanged(report.ImagePaths, (s, e) => TriggerPreviewUpdate());
            _subscriptionManager.SubscribeCollectionChanged(report.FluidLocations, (s, e) => 
            {
                TriggerPreviewUpdate();
                // 当集合变化时，重新订阅新添加项的属性变化
                if (e.NewItems != null)
                {
                    foreach (FluidLocation fluid in e.NewItems)
                    {
                        _subscriptionManager.SubscribePropertyChanged(fluid, (s2, e2) => TriggerPreviewUpdate());
                    }
                }
            });
            
            // 监听现有FluidLocation对象的属性变化
            foreach (var fluid in report.FluidLocations)
            {
                _subscriptionManager.SubscribePropertyChanged(fluid, (s, e) =>
                {
                    TriggerPreviewUpdate();
                    // 积液状态变化时，重新计算O-RADS分级
                    TriggerAutoORadsCalculation(report);
                });
            }

            // 监听HasFluid和UseAutoORads属性变化（注意：Report 的 PropertyChanged 已在 Report setter 中订阅）
            // 这里不需要重复订阅，因为 Report_PropertyChanged 已经处理了所有属性变化

            // 初始化时计算一次O-RADS分级
            TriggerAutoORadsCalculation(report);
        }

        /// <summary>
        /// 触发自动O-RADS分级计算
        /// </summary>
        private void TriggerAutoORadsCalculation(SursReport report)
        {
            if (report != null && report.UseAutoORads)
            {
                report.CalculateAutoORads();
                if (report.AutoORadsResult != null)
                {
                    report.ORadsLevel = report.AutoORadsResult.LevelString;
                }
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
            try
            {
                var filePaths = _dialogService.ShowOpenFileDialogMultiple(
                    "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
                );

                if (filePaths != null)
                {
                    foreach (var filePath in filePaths)
                    {
                        if (Report.ImagePaths.Count < 3)
                        {
                            Report.ImagePaths.Add(filePath);
                            _logger.LogInfo($"已选择图片: {filePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("选择图片失败", ex);
                _dialogService.ShowError("错误", "选择图片时发生错误");
            }
        }

        private void TogglePreview()
        {
            IsPreviewVisible = !IsPreviewVisible;
        }

        private async Task ExportPdfAsync()
        {
            try
            {
                var filePath = _dialogService.ShowSaveFileDialog(
                    "PDF Files (*.pdf)|*.pdf",
                    "pdf",
                    $"SURS_Report_{DateTime.Now:yyyyMMddHHmm}.pdf"
                );

                if (string.IsNullOrEmpty(filePath))
                    return;

                IsExporting = true;
                ExportPdfCommand.NotifyCanExecuteChanged();
                _logger.LogInfo($"开始导出 PDF: {filePath}");

                // 在后台线程生成 PDF，不阻塞 UI
                await Task.Run(() => _pdfService.GeneratePdf(Report, filePath));

                _logger.LogInfo($"PDF 导出成功: {filePath}");
                _dialogService.ShowMessage("导出成功", "PDF 报告已生成");
            }
            catch (Exception ex)
            {
                _logger.LogError("导出 PDF 失败", ex);
                _dialogService.ShowError("导出失败", $"导出 PDF 时发生错误：{ex.Message}");
            }
            finally
            {
                IsExporting = false;
                ExportPdfCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 清理事件订阅
                _subscriptionManager?.Dispose();
                
                // 清理预览更新相关资源
                _previewUpdateSemaphore?.Dispose();
                
                // 清理图片资源
                var oldImage = PreviewImage;
                PreviewImage = null;
                oldImage?.StreamSource?.Dispose();
                
                _logger?.LogDebug("MainViewModel 资源已释放");
            }
            catch (Exception ex)
            {
                _logger?.LogError("释放资源时发生错误", ex);
            }
        }
    }
}
