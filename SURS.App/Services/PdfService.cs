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
                            column.Item().Text(report.HospitalName).FontSize(18).FontFamily("SimSun").AlignCenter();
                            column.Item().Text("妇科超声检查报告单").FontSize(22).Bold().FontFamily("SimSun").AlignCenter();
                            
                            column.Item().PaddingTop(10).Column(headerCol => 
                            {
                                headerCol.Item().Row(row => 
                                {
                                    row.RelativeItem().Text($"登记号: {report.RegistrationNo}");
                                    row.RelativeItem().Text($"姓名: {report.PatientName}");
                                    row.RelativeItem().Text($"性别: {report.Gender}");
                                    row.RelativeItem().Text($"年龄: {report.Age}岁");
                                    row.RelativeItem().Text($"科别: {report.Department}");
                                });
                                
                                headerCol.Item().PaddingTop(5).Row(row => 
                                {
                                    row.RelativeItem().Text($"门诊号: {report.OutpatientNo}");
                                    row.RelativeItem().Text($"住院号: {report.InpatientNo}");
                                    row.RelativeItem().Text($"末次月经: {report.LastMenstrualPeriod?.ToString("yyyy-MM-dd")}"); 
                                });

                                headerCol.Item().PaddingTop(5).Row(row => 
                                {
                                    row.RelativeItem(2).Text($"检查项目: {report.ExamItem}");
                                    row.RelativeItem().AlignRight().Text($"检查日期: {report.ReportDate:yyyy-MM-dd}");
                                });
                                
                                headerCol.Item().PaddingVertical(5).LineHorizontal(1);
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

                            // Construct Findings Paragraph
                            var findings = new System.Text.StringBuilder();

                            // Uterus & Endometrium
                            findings.Append($"子宫{report.Uterus.Position}，子宫体大小约：{report.Uterus.Length}*{report.Uterus.APDiameter}*{report.Uterus.Width}cm，");
                            findings.Append($"宫颈大小约：{report.Uterus.CervixLength}*{report.Uterus.CervixAP}cm。");

                            var myometriumEchoText = report.Uterus.MyometriumEcho == "均匀"
                                ? "均匀"
                                : report.Uterus.MyometriumEcho == "不均"
                                    ? "不均匀"
                                    : string.Empty;

                            if (!string.IsNullOrWhiteSpace(myometriumEchoText))
                            {
                                findings.Append($"肌层回声{myometriumEchoText}");
                            }

                            if (report.Uterus.MyometriumEcho == "不均")
                            {
                                var features = new System.Collections.Generic.List<string>();
                                if (report.Uterus.MyometriumThickeningFocal) features.Add("局限性增厚");
                                if (report.Uterus.MyometriumThickeningDiffuse) features.Add("弥漫性增厚");
                                
                                if (features.Any())
                                {
                                    findings.Append($"，可见{string.Join("、", features)}");
                                }

                                if (report.Uterus.MyometriumThickeningNodule)
                                {
                                    if (features.Any()) findings.Append("；");
                                    else findings.Append("，");
                                    
                                    findings.Append($"{report.Uterus.NoduleLocation}可见{report.Uterus.NoduleCount}{report.Uterus.NoduleType}结节，");
                                    findings.Append($"大小约{report.Uterus.NoduleLength}*{report.Uterus.NoduleWidth}*{report.Uterus.NoduleHeight}cm，");
                                    findings.Append($"边界{report.Uterus.NoduleBoundary}，内呈{report.Uterus.NoduleEcho}");
                                }
                            }
                            findings.Append("。");

                            var endometriumThicknessText = report.Endometrium.CannotMeasure ? "无法测量" : $"{report.Endometrium.Thickness}cm";
                            var endometriumUniformityText = report.Endometrium.EchoUniform ? "回声均匀" : report.Endometrium.EchoNonUniform ? "回声不均匀" : string.Empty;
                            findings.Append($"子宫内膜厚约{endometriumThicknessText}");
                            if (!string.IsNullOrWhiteSpace(endometriumUniformityText))
                                findings.Append($"，{endometriumUniformityText}");
                            if (report.Endometrium.EchoNonUniform)
                            {
                                var nonUniformTypes = new System.Collections.Generic.List<string>();
                                if (report.Endometrium.NonUniformNoCyst) nonUniformTypes.Add("未见囊样结构");
                                if (report.Endometrium.NonUniformRegularCyst) nonUniformTypes.Add("可见形态规则的囊样结构");
                                if (report.Endometrium.NonUniformIrregularCyst) nonUniformTypes.Add("可见不规则的囊样结构");
                                if (nonUniformTypes.Any()) findings.Append($"，{string.Join("，", nonUniformTypes)}");
                            }
                            findings.Append("。");

                            if (report.Endometrium.HasFlow)
                            {
                                findings.Append("子宫内膜CDFI可见血流信号");
                                if (!string.IsNullOrWhiteSpace(report.Endometrium.FlowAmount))
                                    findings.Append($"（{report.Endometrium.FlowAmount}）");
                                if (!string.IsNullOrWhiteSpace(report.Endometrium.FlowPattern))
                                    findings.Append($"，{report.Endometrium.FlowPattern}");
                                findings.Append("。");
                            }
                            else if (report.Endometrium.HasNoFlow)
                            {
                                findings.Append("子宫内膜CDFI未见明显血流信号。");
                            }

                            if (report.Cavity.HasLesion)
                            {
                                findings.Append($"宫腔内{report.Cavity.Location}可见占位性病变，大小约{report.Cavity.Length}*{report.Cavity.APDiameter}*{report.Cavity.Width}cm，");
                                findings.Append($"{(report.Cavity.IsPedunculated ? "带蒂" : "无蒂")}，回声{report.Cavity.EchoType}");
                                if (!string.IsNullOrWhiteSpace(report.Cavity.EchoUniformity))
                                    findings.Append($"，回声{report.Cavity.EchoUniformity}");
                                if (!string.IsNullOrWhiteSpace(report.Cavity.Boundary))
                                    findings.Append($"，边界{report.Cavity.Boundary}");
                                findings.Append("。");

                                if (report.Cavity.HasFlow)
                                {
                                    findings.Append("病灶CDFI可见血流信号");
                                    if (!string.IsNullOrWhiteSpace(report.Cavity.FlowAmount))
                                        findings.Append($"（{report.Cavity.FlowAmount}）");
                                    if (!string.IsNullOrWhiteSpace(report.Cavity.FlowPattern))
                                        findings.Append($"，{report.Cavity.FlowPattern}");
                                    findings.Append("。");
                                }
                                else if (report.Cavity.HasNoFlow)
                                {
                                    findings.Append("病灶CDFI未见明显血流信号。");
                                }
                            }

                            // Ovaries
                            findings.Append("\n");
                            findings.Append($"左卵巢大小约：{report.LeftOvary.Length}*{report.LeftOvary.Width}*{report.LeftOvary.Height}cm，");
                            if (report.LeftOvary.CystCount > 0)
                                findings.Append($"可见{report.LeftOvary.CystCount}个囊肿，最大径{report.LeftOvary.MaxCystDiameter}cm。");
                            else
                                findings.Append("回声未见异常。");

                            findings.Append($"右卵巢大小约：{report.RightOvary.Length}*{report.RightOvary.Width}*{report.RightOvary.Height}cm，");
                            if (report.RightOvary.CystCount > 0)
                                findings.Append($"可见{report.RightOvary.CystCount}个囊肿，最大径{report.RightOvary.MaxCystDiameter}cm。");
                            else
                                findings.Append("回声未见异常。");

                            // Abnormalities
                            findings.Append("\n");
                            bool hasAbnormalities = false;

                            if (report.HasUnilocularCyst)
                            {
                                findings.Append($"可见单房囊肿，位于{report.UnilocularCyst.Location}，大小约{report.UnilocularCyst.Length}*{report.UnilocularCyst.Width}*{report.UnilocularCyst.Height}cm。");
                                hasAbnormalities = true;
                            }
                            if (report.HasMultilocularCyst)
                            {
                                findings.Append($"可见多房囊肿，位于{report.MultilocularCyst.Location}，大小约{report.MultilocularCyst.Length}*{report.MultilocularCyst.Width}*{report.MultilocularCyst.Height}cm。");
                                hasAbnormalities = true;
                            }
                            if (report.HasSolidCyst)
                            {
                                findings.Append($"可见实性成分囊肿，位于{report.SolidCyst.Location}，大小约{report.SolidCyst.Length}*{report.SolidCyst.Width}*{report.SolidCyst.Height}cm。");
                                hasAbnormalities = true;
                            }
                            if (report.HasSolidMass)
                            {
                                findings.Append($"可见实性肿物，位于{report.SolidMass.Location}，大小约{report.SolidMass.Length}*{report.SolidMass.Width}*{report.SolidMass.Height}cm。");
                                hasAbnormalities = true;
                            }
                            if (report.HasFluid)
                            {
                                var fluids = report.FluidLocations.Where(f => f.IsSelected).Select(f => $"{f.Name}深约{f.Depth}cm");
                                if (fluids.Any())
                                {
                                    findings.Append($"盆腔可见积液：{string.Join("，", fluids)}。");
                                    hasAbnormalities = true;
                                }
                            }
                            
                            if (!hasAbnormalities)
                            {
                                findings.Append("盆腔未见明显异常包块及积液。");
                            }

                            x.Item().Text(findings.ToString()).FontSize(12).LineHeight(1.5f);

                            // O-RADS & Conclusion
                            x.Item().PaddingTop(10).LineHorizontal(1);
                            
                            x.Item().Text("超声提示:").FontSize(14).Bold();
                            
                            int hintIndex = 1;
                            if (!string.IsNullOrWhiteSpace(report.UterusDescription))
                                x.Item().Text($"{hintIndex++}. {report.UterusDescription}");

                            // Endometrium Diagnoses
                            var endoDiagnoses = new System.Collections.Generic.List<string>();
                            if (report.IsEndoHyperplasia) endoDiagnoses.Add("子宫内膜增生");
                            if (report.IsEndoPolyp) endoDiagnoses.Add("子宫内膜息肉");
                            if (report.IsEndoCancer) endoDiagnoses.Add("子宫内膜癌");
                            if (report.IsSubmucosalMyoma) endoDiagnoses.Add("子宫黏膜下肌瘤");
                            if (report.IsEndoOther) endoDiagnoses.Add($"其他 ({report.EndoOtherText})");
                            if (endoDiagnoses.Any())
                                x.Item().Text($"{hintIndex++}. 子宫内膜: {string.Join(", ", endoDiagnoses)}");

                            var oradsText = !string.IsNullOrWhiteSpace(report.ORadsLevel) ? report.ORadsLevel : report.ORadsScore;
                            if (!string.IsNullOrWhiteSpace(oradsText))
                                x.Item().PaddingTop(5).Text($"O-RADS 分级: {oradsText}").Bold().FontSize(14).FontColor(Colors.Red.Medium);
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            });
        }

        public void GeneratePdf(SursReport report, string filePath)
        {
            GetDocument(report).GeneratePdf(filePath);
        }

        /// <summary>
        /// 生成预览图片（第一页）
        /// </summary>
        public byte[]? GeneratePreviewImage(SursReport report, int dpi = 150)
        {
            try
            {
                var document = GetDocument(report);
                var images = document.GenerateImages(new ImageGenerationSettings
                {
                    ImageFormat = ImageFormat.Png
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
