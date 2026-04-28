using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SURS.App.Models;
using System.Linq;
using System.IO;

namespace SURS.App.Services
{
    public class PdfService
    {
        private IDocument GetDocument(SursReport report)
        {
            // QuestPDF License configuration (Community is free for individuals/small companies)
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Microsoft YaHei"));

                    page.Header()
                        .Column(column => 
                        {
                            column.Item().Text(report.HospitalName).FontSize(22).Bold().FontFamily("SimSun").AlignCenter();
                            
                            column.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.5f); // 登记号
                                    columns.RelativeColumn(1);    // Spacer/Title
                                    columns.RelativeColumn(1);    // Title
                                    columns.RelativeColumn(1.5f); // Title
                                    columns.RelativeColumn(2);    // 序号
                                });

                                // Row 1
                                table.Cell().Row(1).Column(1).PaddingVertical(2).Text($"登记号: {report.RegistrationNo}");
                                
                                table.Cell().Row(1).Column(2).ColumnSpan(3).PaddingVertical(2).AlignCenter()
                                     .Text("妇科超声检查报告单").FontSize(18).Bold().FontFamily("SimSun");
                                     
                                table.Cell().Row(1).Column(5).PaddingVertical(2).AlignRight().Text($"序号: {report.SerialNo}");
                                
                                table.Cell().Row(1).Column(1).ColumnSpan(5).BorderBottom(1).BorderColor(Colors.Black);

                                // Row 2
                                table.Cell().Row(2).Column(1).PaddingVertical(2).Text($"姓名: {report.PatientName}");
                                table.Cell().Row(2).Column(2).PaddingVertical(2).Text($"性别: {report.Gender}");
                                table.Cell().Row(2).Column(3).PaddingVertical(2).Text($"年龄: {report.Age}岁");
                                table.Cell().Row(2).Column(4).PaddingVertical(2).Text($"科别: {report.Department}");
                                table.Cell().Row(2).Column(5).PaddingVertical(2).AlignRight().Text($"门诊号: {report.OutpatientNo}");

                  

                                // Row 3
                                table.Cell().Row(3).Column(1).PaddingVertical(2).Text($"住院号: {report.InpatientNo}");
                                table.Cell().Row(3).Column(2).PaddingVertical(2).Text($"床位: {report.BedNo}");
                                
                                var lmpText = report.LastMenstrualPeriod?.ToString("yyyy-MM-dd");
                                if (string.IsNullOrEmpty(lmpText) && (report.LmpYear.HasValue || report.LmpMonth.HasValue || report.LmpDay.HasValue))
                                {
                                    lmpText = $"{report.LmpYear?.ToString() ?? "____"}-{report.LmpMonth?.ToString("00") ?? "__"}-{report.LmpDay?.ToString("00") ?? "__"}";
                                }
                                table.Cell().Row(3).Column(5).PaddingVertical(2).AlignRight().Text($"末次月经: {lmpText}");

                               

                                // Row 4
                                table.Cell().Row(4).Column(1).ColumnSpan(4).PaddingVertical(2).Text($"检查项目: {report.ExamItem}");
                                table.Cell().Row(4).Column(5).PaddingVertical(2).AlignRight().Text($"申请医师: {report.ApplyingPhysician}");

                                table.Cell().Row(4).Column(1).ColumnSpan(5).BorderBottom(1).BorderColor(Colors.Black);
                            });
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(10);

                            // Images
                            if (report.ImagePaths.Any())
                            {
                                x.Item().Row(row => 
                                {
                                    row.Spacing(10);
                                    foreach (var imgPath in report.ImagePaths)
                                    {
                                        row.RelativeItem().Image(imgPath).FitArea();
                                    }
                                });
                                x.Item().PaddingBottom(10);
                            }

                            x.Item().Text("超声所见:").FontSize(14).Bold();

                            var findingsBody = string.IsNullOrWhiteSpace(report.EditedFindingsText)
                                ? ReportNarrativeTextBuilder.BuildAutoFindingsText(report)
                                : report.EditedFindingsText;
                            x.Item().Text(findingsBody).FontSize(12).LineHeight(1.5f);

                            // O-RADS & Conclusion
                            x.Item().PaddingTop(10).LineHorizontal(1);
                            
                            x.Item().Text("超声提示:").FontSize(14).Bold();

                            if (!string.IsNullOrWhiteSpace(report.EditedImpressionText))
                            {
                                x.Item().Text(report.EditedImpressionText).FontSize(12).LineHeight(1.5f);
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(report.UterusDescription))
                                    x.Item().Text(report.UterusDescription);

                                // Endometrium Diagnoses - 直接显示选项内容，不加编号
                                var endoDiagnoses = new System.Collections.Generic.List<string>();
                                if (report.IsEndoHyperplasia) endoDiagnoses.Add("子宫内膜增生");
                                if (report.IsEndoPolyp) endoDiagnoses.Add("子宫内膜息肉");
                                if (report.IsEndoCancer) endoDiagnoses.Add("子宫内膜癌");
                                if (report.IsSubmucosalMyoma) endoDiagnoses.Add("子宫黏膜下肌瘤");
                                if (report.IsEndoOther && !string.IsNullOrWhiteSpace(report.EndoOtherText)) endoDiagnoses.Add(report.EndoOtherText);
                                if (endoDiagnoses.Any())
                                    x.Item().Text(string.Join("，", endoDiagnoses));

                                var oradsText = !string.IsNullOrWhiteSpace(report.ORadsLevel) ? report.ORadsLevel : report.ORadsScore;
                                if (!string.IsNullOrWhiteSpace(oradsText))
                                    x.Item().PaddingTop(5).Text($"{oradsText}").Bold().FontSize(14).FontColor(Colors.Red.Medium);
                            }
                        });

                    page.Footer()
                        .Column(column =>
                        {
                            column.Item().LineHorizontal(1);
                            
                            // 备注
                            column.Item().PaddingTop(5).Text(text => 
                            {
                                text.Span("备注：").Bold();
                                if (!string.IsNullOrWhiteSpace(report.Remarks))
                                    text.Span(report.Remarks);
                            });
                            
                            // 录入员、诊断医师、时间
                            column.Item().PaddingTop(5).Row(row => 
                            {
                                row.RelativeItem().Text(text => 
                                {
                                    text.Span("录入员：").Bold();
                                    if (!string.IsNullOrWhiteSpace(report.Typist))
                                        text.Span(report.Typist);
                                });
                                
                                row.RelativeItem().Text(text => 
                                {
                                    text.Span("诊断医师：").Bold();
                                    if (!string.IsNullOrWhiteSpace(report.Diagnostician))
                                        text.Span(report.Diagnostician);
                                });
                                
                                row.RelativeItem().AlignRight().Text(text => 
                                {
                                    text.Span("时间：").Bold();
                                    text.Span(report.ReportDate.ToString("yyyy-MM-dd HH:mm:ss"));
                                });
                            });
                            
                            column.Item().PaddingTop(5).Text("注：本报告仅供临床参考").FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                });
            });
        }

        public void GeneratePdf(SursReport report, string filePath)
        {
            GetDocument(report).GeneratePdf(filePath);
        }

        /// <summary>
        /// 生成预览图片（第一页）。使用较低 DPI 以加快预览刷新。
        /// </summary>
        public byte[]? GeneratePreviewImage(SursReport report, int dpi = 288)
        {
            try
            {
                var document = GetDocument(report);
                var images = document.GenerateImages(new ImageGenerationSettings
                {
                    ImageFormat = ImageFormat.Png,
                    RasterDpi = dpi
                });

                if (images != null)
                {
                    // GenerateImages 返回 IEnumerable<byte[]>
                    var imageList = images.ToList();
                    if (imageList.Count > 0)
                    {
                        return imageList[0];
                    }
                }
            }
            catch
            {
                // 如果生成失败，返回null
            }
            return null;
        }
    }
}
