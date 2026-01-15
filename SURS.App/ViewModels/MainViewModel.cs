using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SURS.App.Models;
using SURS.App.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;

namespace SURS.App.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly PdfService _pdfService;

        public SursReport Report { get; }

        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand SelectImageCommand { get; }

        public MainViewModel()
        {
            _pdfService = new PdfService();
            Report = new SursReport();
            InitializeData();

            ExportPdfCommand = new RelayCommand(ExportPdf);
            SelectImageCommand = new RelayCommand(SelectImage);
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
