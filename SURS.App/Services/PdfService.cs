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

                            var uterusPara = new System.Text.StringBuilder();
                            uterusPara.Append($"子宫{report.Uterus.Position}，子宫体大小约：{report.Uterus.Length}*{report.Uterus.APDiameter}*{report.Uterus.Width}cm，");
                            uterusPara.Append($"宫颈大小约：{report.Uterus.CervixLength}*{report.Uterus.CervixAP}cm ");

                            var myometriumEchoText = report.Uterus.MyometriumEcho == "均匀"
                                ? "均匀"
                                : report.Uterus.MyometriumEcho == "不均"
                                    ? "不均匀"
                                    : string.Empty;

                            if (!string.IsNullOrWhiteSpace(myometriumEchoText))
                                uterusPara.Append($"肌层回声：{myometriumEchoText}");

                            if (report.Uterus.MyometriumEcho == "不均")
                            {
                                var features = new System.Collections.Generic.List<string>();
                                if (report.Uterus.MyometriumThickeningFocal) features.Add("局限性增厚");
                                if (report.Uterus.MyometriumThickeningDiffuse) features.Add("弥漫性增厚");

                                if (features.Any())
                                    uterusPara.Append($"，可见{string.Join("、", features)}");

                                if (report.Uterus.MyometriumThickeningNodule)
                                {
                                    if (features.Any()) uterusPara.Append("；");
                                    else uterusPara.Append("，");

                                    uterusPara.Append($"{report.Uterus.NoduleLocation}可见{report.Uterus.NoduleCount}{report.Uterus.NoduleType}结节，");
                                    uterusPara.Append($"大小约{report.Uterus.NoduleLength}*{report.Uterus.NoduleWidth}*{report.Uterus.NoduleHeight}cm，");
                                    uterusPara.Append($"边界{report.Uterus.NoduleBoundary}，内呈{report.Uterus.NoduleEcho}");
                                }
                            }
                            uterusPara.Append("。");

                            var endometriumPara = new System.Text.StringBuilder();
                            var endometriumThicknessText = report.Endometrium.CannotMeasure ? "无法测量" : $"{report.Endometrium.Thickness}cm";
                            var endometriumUniformityText = report.Endometrium.EchoUniform ? "回声：均匀" : report.Endometrium.EchoNonUniform ? "回声：不均匀" : string.Empty;
                            endometriumPara.Append($"子宫内膜厚{endometriumThicknessText}");
                            if (!string.IsNullOrWhiteSpace(report.Endometrium.EchoType))
                                endometriumPara.Append($"，回声：{report.Endometrium.EchoType}");
                            if (!string.IsNullOrWhiteSpace(endometriumUniformityText))
                                endometriumPara.Append($"，{endometriumUniformityText}");
                            if (report.Endometrium.EchoNonUniform)
                            {
                                var nonUniformTypes = new System.Collections.Generic.List<string>();
                                if (report.Endometrium.NonUniformNoCyst) nonUniformTypes.Add("未见囊样结构");
                                if (report.Endometrium.NonUniformRegularCyst) nonUniformTypes.Add("可见形态规则的囊样结构");
                                if (report.Endometrium.NonUniformIrregularCyst) nonUniformTypes.Add("可见不规则的囊样结构");
                                if (nonUniformTypes.Any()) endometriumPara.Append($"，{string.Join("，", nonUniformTypes)}");
                            }
                            if (!string.IsNullOrWhiteSpace(report.Endometrium.Midline))
                                endometriumPara.Append($"，宫腔线呈{report.Endometrium.Midline}");
                            if (!string.IsNullOrWhiteSpace(report.Endometrium.JunctionalZone))
                                endometriumPara.Append($"，结合带{report.Endometrium.JunctionalZone}");
                            endometriumPara.Append("。");
                            var endoCdfiText = string.Empty;

                            if (report.Endometrium.HasFlow)
                            {
                                var endoCdifPara = new System.Text.StringBuilder();
                                endoCdifPara.Append("子宫内膜CDFI可见血流信号");
                                if (!string.IsNullOrWhiteSpace(report.Endometrium.FlowAmount))
                                    endoCdifPara.Append($"（{report.Endometrium.FlowAmount}）");
                                if (!string.IsNullOrWhiteSpace(report.Endometrium.FlowPattern))
                                    endoCdifPara.Append($"，{report.Endometrium.FlowPattern}");
                                endoCdifPara.Append("。");
                                endoCdfiText = endoCdifPara.ToString();
                            }
                            else if (report.Endometrium.HasNoFlow)
                            {
                                endoCdfiText = "子宫内膜CDFI未见明显血流信号。";
                            }

                            string? cavityText = null;
                            if (report.Cavity.HasLesion)
                            {
                                var cavityPara = new System.Text.StringBuilder();
                                cavityPara.Append($"宫腔内可见占位性病变，位于{report.Cavity.Location}，大小约{report.Cavity.Length}*{report.Cavity.APDiameter}*{report.Cavity.Width}cm，");
                                cavityPara.Append($"{(report.Cavity.IsPedunculated ? "带蒂" : "无蒂")}，回声：{report.Cavity.EchoType}");
                                if (!string.IsNullOrWhiteSpace(report.Cavity.EchoUniformity))
                                    cavityPara.Append($"，{report.Cavity.EchoUniformity}");
                                if (!string.IsNullOrWhiteSpace(report.Cavity.Boundary))
                                    cavityPara.Append($"，边界{report.Cavity.Boundary}");
                                cavityPara.Append("。");

                                if (report.Cavity.HasFlow)
                                {
                                    cavityPara.Append("病灶CDFI可见血流信号");
                                    if (!string.IsNullOrWhiteSpace(report.Cavity.FlowAmount))
                                        cavityPara.Append($"（{report.Cavity.FlowAmount}）");
                                    if (!string.IsNullOrWhiteSpace(report.Cavity.FlowPattern))
                                        cavityPara.Append($"，{report.Cavity.FlowPattern}");
                                    cavityPara.Append("。");
                                }
                                else if (report.Cavity.HasNoFlow)
                                {
                                    cavityPara.Append("病灶CDFI未见明显血流信号。");
                                }
                                cavityText = cavityPara.ToString();
                            }

                            var ovaryPara = new System.Text.StringBuilder();
                            if (report.IsOvaryNormal)
                            {
                                ovaryPara.Append($"左卵巢大小约：{report.LeftOvary.Length}*{report.LeftOvary.Width}*{report.LeftOvary.Height}cm，");
                                if (report.LeftOvary.CystCount > 0)
                                    ovaryPara.Append($"其内见{report.LeftOvary.CystCount}个囊性无回声，较大者直径{report.LeftOvary.MaxCystDiameter}cm。");
                                else
                                    ovaryPara.Append("回声：未见异常。");

                                ovaryPara.Append($"右卵巢大小约：{report.RightOvary.Length}*{report.RightOvary.Width}*{report.RightOvary.Height}cm，");
                                if (report.RightOvary.CystCount > 0)
                                    ovaryPara.Append($"其内见{report.RightOvary.CystCount}个囊性无回声，较大者直径{report.RightOvary.MaxCystDiameter}cm。");
                                else
                                    ovaryPara.Append("回声未见异常。");
                            }

                            var abnormalPara = new System.Text.StringBuilder();
                            bool hasAbnormalities = false;

                            void AppendAbnormal(string text)
                            {
                                if (string.IsNullOrWhiteSpace(text)) return;
                                if (abnormalPara.Length > 0 && !abnormalPara.ToString().EndsWith("。"))
                                    abnormalPara.Append("。");
                                if (abnormalPara.Length > 0) abnormalPara.Append(" ");
                                abnormalPara.Append(text);
                            }

                            if (report.HasUnilocularCyst)
                            {
                                var unilocularLocation = report.UnilocularCyst.Location == "其他" && !string.IsNullOrWhiteSpace(report.UnilocularCyst.LocationOther)
                                    ? report.UnilocularCyst.LocationOther
                                    : report.UnilocularCyst.Location;

                                var echoes = new System.Collections.Generic.List<string>();
                                if (report.UnilocularCyst.IsSimpleCyst) echoes.Add("单纯性囊肿");
                                if (report.UnilocularCyst.IsNonSimpleCyst) echoes.Add("非单纯性囊肿");
                                if (report.UnilocularCyst.EchoSmoothWall) echoes.Add("囊肿内壁光滑");
                                if (report.UnilocularCyst.EchoRoughWall) echoes.Add("囊肿内壁不光滑");
                                if (report.UnilocularCyst.EchoDenseDots) echoes.Add("内见密集细点状回声");
                                if (report.UnilocularCyst.EchoFlocculent) echoes.Add("内见絮状回声");
                                if (report.UnilocularCyst.EchoGrid) echoes.Add("内见网格样回声");
                                if (report.UnilocularCyst.EchoStrongMass) echoes.Add("内见强回声团");
                                if (report.UnilocularCyst.EchoShortLines) echoes.Add("内见短线样强回声");
                                if (report.UnilocularCyst.EchoWeakDots) echoes.Add("内见弱点状回声");
                                if (report.UnilocularCyst.EchoPatchy) echoes.Add("内见片状回声");
                                if (!string.IsNullOrWhiteSpace(report.UnilocularCyst.EchoOther)) echoes.Add($"{report.UnilocularCyst.EchoOther}");

                                var uni = new System.Text.StringBuilder();
                                uni.Append($"有单房囊肿，大小约{report.UnilocularCyst.Length}*{report.UnilocularCyst.Width}*{report.UnilocularCyst.Height}cm，位置{unilocularLocation}");
                                if (echoes.Any()) uni.Append($"，回声：{string.Join("，", echoes)}");
                                if (!string.IsNullOrWhiteSpace(report.UnilocularCyst.Boundary)) uni.Append($"，边界{report.UnilocularCyst.Boundary}");
                                if (!string.IsNullOrWhiteSpace(report.UnilocularCyst.Shadow)) uni.Append($"，声影{report.UnilocularCyst.Shadow}");
                                if (report.UnilocularCyst.BloodFlowScore > 0) uni.Append($"，血流评分{report.UnilocularCyst.BloodFlowScore}");
                                AppendAbnormal(uni.ToString());
                                hasAbnormalities = true;
                            }
                            if (report.HasMultilocularCyst)
                            {
                                var multilocularLocation = report.MultilocularCyst.Location == "其他" && !string.IsNullOrWhiteSpace(report.MultilocularCyst.LocationOther)
                                    ? report.MultilocularCyst.LocationOther
                                    : report.MultilocularCyst.Location;

                                var echoes = new System.Collections.Generic.List<string>();
                                if (report.MultilocularCyst.EchoSmoothWall) echoes.Add("囊肿内壁光滑");
                                if (report.MultilocularCyst.EchoRoughWall) echoes.Add("囊肿内壁不光滑");
                                if (report.MultilocularCyst.EchoSmoothSeptum) echoes.Add("分隔光滑");
                                if (report.MultilocularCyst.EchoRoughSeptum) echoes.Add("分隔不光滑");
                                if (report.MultilocularCyst.EchoGoodTransmission) echoes.Add("透声好");
                                if (report.MultilocularCyst.EchoPoorTransmission) echoes.Add("透声差");
                                if (report.MultilocularCyst.EchoDenseDots) echoes.Add("内见密集细点状回声");
                                if (report.MultilocularCyst.EchoFlocculent) echoes.Add("内见絮状回声");
                                if (report.MultilocularCyst.EchoStrongMass) echoes.Add("内见强回声团");
                                if (report.MultilocularCyst.EchoShortLines) echoes.Add("内见短线样强回声");
                                if (report.MultilocularCyst.EchoWeakDots) echoes.Add("内见弱点状回声");
                                if (report.MultilocularCyst.EchoPatchy) echoes.Add("内见片状回声");
                                if (report.MultilocularCyst.EchoRegularInnerWall) echoes.Add("内壁规则");
                                if (report.MultilocularCyst.EchoIrregularInnerWall) echoes.Add("内壁不规则");
                                if (report.MultilocularCyst.EchoMoreThan10Locules) echoes.Add("超过十个囊腔");
                                if (!string.IsNullOrWhiteSpace(report.MultilocularCyst.EchoOther)) echoes.Add($"{report.MultilocularCyst.EchoOther}");

                                var flows = new System.Collections.Generic.List<string>();
                                if (report.MultilocularCyst.FlowOnSeptum) flows.Add("分隔血流");
                                if (report.MultilocularCyst.FlowOnWall) flows.Add("囊壁血流");

                                var multi = new System.Text.StringBuilder();
                                multi.Append($"有多房囊肿，大小约{report.MultilocularCyst.Length}*{report.MultilocularCyst.Width}*{report.MultilocularCyst.Height}cm，位置{multilocularLocation}");
                                if (echoes.Any()) multi.Append($"，回声：{string.Join("，", echoes)}");
                                if (!string.IsNullOrWhiteSpace(report.MultilocularCyst.Boundary)) multi.Append($"，边界{report.MultilocularCyst.Boundary}");
                                if (!string.IsNullOrWhiteSpace(report.MultilocularCyst.Shadow)) multi.Append($"，{report.MultilocularCyst.Shadow}声影");
                                if (report.MultilocularCyst.BloodFlowScore > 0) multi.Append($"，血流评分{report.MultilocularCyst.BloodFlowScore}");
                                if (flows.Any()) multi.Append($"，血流分布：{string.Join("，", flows)}");
                                AppendAbnormal(multi.ToString());
                                hasAbnormalities = true;
                            }
                            if (report.HasSolidCyst)
                            {
                                var solidCystLocation = report.SolidCyst.Location == "其他" && !string.IsNullOrWhiteSpace(report.SolidCyst.LocationOther)
                                    ? report.SolidCyst.LocationOther
                                    : report.SolidCyst.Location;

                                var echoes = new System.Collections.Generic.List<string>();
                                if (report.SolidCyst.EchoSmoothWall) echoes.Add("囊肿内壁光滑");
                                if (report.SolidCyst.EchoRoughWall) echoes.Add("囊肿内壁不光滑");
                                if (report.SolidCyst.EchoSmoothSeptum) echoes.Add("分隔光滑");
                                if (report.SolidCyst.EchoRoughSeptum) echoes.Add("分隔不光滑");
                                if (report.SolidCyst.EchoGoodTransmission) echoes.Add("透声好");
                                if (report.SolidCyst.EchoPoorTransmission) echoes.Add("透声差");
                                if (report.SolidCyst.EchoDenseDots) echoes.Add("内见密集细点状回声");
                                if (report.SolidCyst.EchoFlocculent) echoes.Add("内见絮状回声");
                                if (report.SolidCyst.EchoGrid) echoes.Add("内见网格样回声");
                                if (report.SolidCyst.EchoStrongMass) echoes.Add("内见强回声团");
                                if (report.SolidCyst.EchoShortLines) echoes.Add("内见短线样强回声");
                                if (report.SolidCyst.EchoWeakDots) echoes.Add("内见弱点状回声");
                                if (report.SolidCyst.EchoPatchy) echoes.Add("内见片状回声");
                                if (report.SolidCyst.EchoMoreThan10Locules) echoes.Add("超过十个囊腔");
                                if (!string.IsNullOrWhiteSpace(report.SolidCyst.EchoOther)) echoes.Add($"{report.SolidCyst.EchoOther}");

                                var solid = new System.Text.StringBuilder();
                                solid.Append($"存在实性成分的囊肿，大小约{report.SolidCyst.Length}*{report.SolidCyst.Width}*{report.SolidCyst.Height}cm，位置{solidCystLocation}");
                                if (echoes.Any()) solid.Append($"，囊性部分回声：{string.Join("，", echoes)}");
                                if (report.SolidCyst.HasPapillary)
                                {
                                    var papEchoes = new System.Collections.Generic.List<string>();
                                    if (report.SolidCyst.PapillaryEchoLow) papEchoes.Add("低回声");
                                    if (report.SolidCyst.PapillaryEchoIso) papEchoes.Add("等回声");
                                    if (report.SolidCyst.PapillaryEchoHigh) papEchoes.Add("高回声");

                                    solid.Append($"，乳头：数量{report.SolidCyst.PapillaryCount}，最大高度{report.SolidCyst.PapillaryHeightVal}cm");
                                    if (papEchoes.Any()) solid.Append($"，回声{string.Join("/", papEchoes)}");
                                    if (!string.IsNullOrWhiteSpace(report.SolidCyst.PapillaryContour)) solid.Append($"，轮廓{report.SolidCyst.PapillaryContour}");
                                    if (!string.IsNullOrWhiteSpace(report.SolidCyst.PapillaryShadow)) solid.Append($"，声影{report.SolidCyst.PapillaryShadow}");
                                    if (report.SolidCyst.PapillaryHasFlow && !string.IsNullOrWhiteSpace(report.SolidCyst.PapillaryFlowAmount))
                                        solid.Append($"，血流{report.SolidCyst.PapillaryFlowAmount}");
                                    else if (report.SolidCyst.PapillaryHasNoFlow)
                                        solid.Append("，未见血流");
                                }
                                if (report.SolidCyst.SolidLength > 0 || report.SolidCyst.SolidWidth > 0 || report.SolidCyst.SolidHeight > 0)
                                {
                                    var solidEchoes = new System.Collections.Generic.List<string>();
                                    if (report.SolidCyst.SolidEchoLow) solidEchoes.Add("低回声");
                                    if (report.SolidCyst.SolidEchoIso) solidEchoes.Add("等回声");
                                    if (report.SolidCyst.SolidEchoHigh) solidEchoes.Add("高回声");
                                    if (report.SolidCyst.SolidEchoOther && !string.IsNullOrWhiteSpace(report.SolidCyst.SolidEchoOtherText)) solidEchoes.Add($"{report.SolidCyst.SolidEchoOtherText}");

                                    solid.Append($"，实性成分大小约{report.SolidCyst.SolidLength}*{report.SolidCyst.SolidWidth}*{report.SolidCyst.SolidHeight}cm");
                                    if (solidEchoes.Any()) solid.Append($"，回声{string.Join("/", solidEchoes)}");
                                    if (!string.IsNullOrWhiteSpace(report.SolidCyst.SolidBoundary)) solid.Append($"，边界{report.SolidCyst.SolidBoundary}");
                                    if (!string.IsNullOrWhiteSpace(report.SolidCyst.SolidShadow)) solid.Append($"，{report.SolidCyst.SolidShadow}声影");
                                    if (report.SolidCyst.SolidHasFlow && !string.IsNullOrWhiteSpace(report.SolidCyst.SolidFlowAmount))
                                        solid.Append($"，血流{report.SolidCyst.SolidFlowAmount}");
                                    else if (report.SolidCyst.SolidHasNoFlow)
                                        solid.Append("，未见血流");
                                }
                                if (!string.IsNullOrWhiteSpace(report.SolidCyst.Boundary)) solid.Append($"，病灶边界{report.SolidCyst.Boundary}");
                                if (report.SolidCyst.BloodFlowScore > 0) solid.Append($"，整体血流评分{report.SolidCyst.BloodFlowScore}");
                                AppendAbnormal(solid.ToString());
                                hasAbnormalities = true;
                            }
                            if (report.HasSolidMass)
                            {
                                var solidMassLocation = report.SolidMass.Location == "" && !string.IsNullOrWhiteSpace(report.SolidMass.LocationOther)
                                    ? report.SolidMass.LocationOther
                                    : report.SolidMass.Location;

                                var mass = new System.Text.StringBuilder();
                                mass.Append($"实性肿物，大小约{report.SolidMass.Length}*{report.SolidMass.Width}*{report.SolidMass.Height}cm，位置{solidMassLocation}");
                                if (!string.IsNullOrWhiteSpace(report.SolidMass.EchoUniformity)) mass.Append($"，回声{report.SolidMass.EchoUniformity}");
                                if (!string.IsNullOrWhiteSpace(report.SolidMass.Boundary)) mass.Append($"，边界{report.SolidMass.Boundary}");
                                if (!string.IsNullOrWhiteSpace(report.SolidMass.Shadow)) mass.Append($"，{report.SolidMass.Shadow}声影");
                                if (!string.IsNullOrWhiteSpace(report.SolidMass.EchoType)) mass.Append($"，回声类型{report.SolidMass.EchoType}");
                                if (report.SolidMass.BloodFlowScore > 0) mass.Append($"，血流评分{report.SolidMass.BloodFlowScore}");
                                AppendAbnormal(mass.ToString());
                                hasAbnormalities = true;
                            }
                            if (report.HasFluid)
                            {
                                var fluids = report.FluidLocations.Where(f => f.IsSelected).Select(f => $"{f.Name}深约{f.Depth}cm").ToList();
                                if (report.HasFluidOther && !string.IsNullOrWhiteSpace(report.FluidOtherLocation)) fluids.Add($"{report.FluidOtherLocation}");
                                if (fluids.Any())
                                {
                                    AppendAbnormal($"宫腔可见积液：{string.Join("，", fluids)}");
                                    hasAbnormalities = true;
                                }
                            }

                            if (!hasAbnormalities)
                                abnormalPara.Append("宫腔未见明显病变。");
                            if (!abnormalPara.ToString().EndsWith("。")) abnormalPara.Append("。");

                            var findingsText = new System.Text.StringBuilder();
                            findingsText.Append(uterusPara);
                            findingsText.Append("\n\n");
                            findingsText.Append(endometriumPara);
                            if (!string.IsNullOrWhiteSpace(endoCdfiText))
                            {
                                findingsText.Append("\n");
                                findingsText.Append(endoCdfiText);
                            }
                            if (!string.IsNullOrWhiteSpace(cavityText))
                            {
                                findingsText.Append("\n\n");
                                findingsText.Append(cavityText);
                            }
                            findingsText.Append("\n\n");
                            if (report.IsOvaryNormal && ovaryPara.Length > 0)
                            {
                                findingsText.Append(ovaryPara);
                                findingsText.Append("\n");
                            }
                            findingsText.Append(abnormalPara);

                            x.Item().Text(findingsText.ToString()).FontSize(12).LineHeight(1.5f);

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
                            if (report.IsEndoOther) endoDiagnoses.Add($"({report.EndoOtherText})");
                            if (endoDiagnoses.Any())
                                x.Item().Text($"{hintIndex++}. 子宫内膜: {string.Join(", ", endoDiagnoses)}");

                            var oradsText = !string.IsNullOrWhiteSpace(report.ORadsLevel) ? report.ORadsLevel : report.ORadsScore;
                            if (!string.IsNullOrWhiteSpace(oradsText))
                                x.Item().PaddingTop(5).Text($"{oradsText}").Bold().FontSize(14).FontColor(Colors.Red.Medium);
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
