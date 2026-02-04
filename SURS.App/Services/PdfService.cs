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

                            var findingsText = new System.Text.StringBuilder();

                            // 子宫部分
                            if (report.IncludeUterus)
                            {
                                var uterusPara = new System.Text.StringBuilder();
                                uterusPara.Append($"子宫{report.Uterus.Position}，子宫体大小约：{report.Uterus.Length}*{report.Uterus.APDiameter}*{report.Uterus.Width}cm，");
                                if (report.Uterus.ShouldMeasureCervix && (report.Uterus.CervixLength > 0 || report.Uterus.CervixAP > 0))
                                {
                                    uterusPara.Append($"宫颈大小约：{report.Uterus.CervixLength}*{report.Uterus.CervixAP}cm ");
                                }

                            var myometriumEchoText = report.Uterus.MyometriumEcho == "均匀"
                                ? "均匀"
                                : report.Uterus.MyometriumEcho == "不均"
                                    ? "不均匀"
                                    : string.Empty;

                            if (!string.IsNullOrWhiteSpace(myometriumEchoText))
                                uterusPara.Append($"肌层回声{myometriumEchoText}");

                            if (report.Uterus.MyometriumEcho == "不均")
                            {
                                var features = new System.Collections.Generic.List<string>();
                                if (report.Uterus.MyometriumThickeningFocal) features.Add("局限性增厚");
                                if (report.Uterus.MyometriumThickeningDiffuse) features.Add("弥漫性增厚");

                                if (features.Any())
                                    uterusPara.Append($"，可见{string.Join("、", features)}");

                                if (report.Uterus.MyometriumThickeningNodule)
                                {
                                    var nodules = report.Uterus.Nodules
                                        .Where(n => n != null && !n.IsEmpty)
                                        .ToList();

                                    if (nodules.Any())
                                    {
                                        if (features.Any()) uterusPara.Append("；");
                                        else uterusPara.Append("，");

                                        string DescribeNodule(MyometriumNodule n)
                                        {
                                            var parts = new System.Collections.Generic.List<string>();

                                            if (!string.IsNullOrWhiteSpace(n.Location))
                                                parts.Add($"位于{n.Location}");

                                            var hasSize = n.Length > 0 && n.Width > 0 && n.Height > 0;
                                            if (hasSize)
                                                parts.Add($"大小约{n.Length}*{n.Width}*{n.Height}cm");

                                            if (!string.IsNullOrWhiteSpace(n.Echo))
                                                parts.Add($"{n.Echo}");

                                            if (!string.IsNullOrWhiteSpace(n.Boundary))
                                                parts.Add($"边界{n.Boundary}");

                                            if (n.Protrudes)
                                                parts.Add("外突");

                                            if (n.CompressesCavity)
                                                parts.Add("压迫宫腔");

                                            return string.Join("，", parts);
                                        }

                                        // 多发且“只描述较大者”时，按体积（长×宽×高）自动取最大的一条；否则全部写出
                                        var toOutput = nodules;
                                        if (nodules.Count >= 2 && report.Uterus.ReportOnlyLargestNodule)
                                        {
                                            var byVolume = nodules.OrderByDescending(n => n.Length * n.Width * n.Height).FirstOrDefault();
                                            if (byVolume != null)
                                                toOutput = new System.Collections.Generic.List<MyometriumNodule> { byVolume };
                                        }

                                        if (toOutput.Count > 1)
                                            uterusPara.Append("可见多发结节");
                                        else if (nodules.Count >= 2)
                                            uterusPara.Append("可见多发结节，较大者");
                                        else
                                            uterusPara.Append("可见结节");

                                        var details = toOutput
                                            .Select(DescribeNodule)
                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                            .ToList();

                                        if (details.Any())
                                            uterusPara.Append($"，{string.Join("；", details)}");
                                    }
                                }
                            }
                            uterusPara.Append("。");
                            
                            if (uterusPara.Length > 0)
                            {
                                findingsText.Append(uterusPara);
                                findingsText.Append("\n\n");
                            }
                            }

                            // 子宫内膜部分
                            if (report.IncludeEndometrium)
                            {
                                var endometriumPara = new System.Text.StringBuilder();
                                var endometriumThicknessText = report.Endometrium.CannotMeasure ? "无法测量" : $"{report.Endometrium.Thickness}cm";
                                if (report.Endometrium.IsSingleLayer)
                                    endometriumThicknessText += "（单层）";
                                else
                                    endometriumThicknessText += "（双层）";
                                var endometriumUniformityText = report.Endometrium.EchoUniform ? "回声：均匀" : report.Endometrium.EchoNonUniform ? "回声：不均匀" : string.Empty;
                                endometriumPara.Append($"子宫内膜厚{endometriumThicknessText}");
                                if (!string.IsNullOrWhiteSpace(report.Endometrium.EchoType))
                                    endometriumPara.Append($"，{report.Endometrium.EchoType}");
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
                                {
                                    if (report.Endometrium.Midline == "线性")
                                        endometriumPara.Append("，宫腔线呈线性");
                                    else if (report.Endometrium.Midline == "非线性")
                                        endometriumPara.Append("，宫腔线呈非线性");
                                    else
                                        endometriumPara.Append($"，宫腔线{report.Endometrium.Midline}");
                                }
                                if (!string.IsNullOrWhiteSpace(report.Endometrium.JunctionalZone))
                                    endometriumPara.Append($"，结合带{report.Endometrium.JunctionalZone}");
                                endometriumPara.Append("。");
                                
                                if (endometriumPara.Length > 0)
                                {
                                    findingsText.Append(endometriumPara);
                                }
                                
                                var endoCdfiText = string.Empty;
                                if (report.Endometrium.ShouldMeasureFlow)
                                {
                                    if (report.Endometrium.HasFlow)
                                    {
                                        var endoCdifPara = new System.Text.StringBuilder();
                                        endoCdifPara.Append("子宫内膜CDFI可见血流信号");
                                        if (!string.IsNullOrWhiteSpace(report.Endometrium.FlowAmount))
                                            endoCdifPara.Append($"（{report.Endometrium.FlowAmount}）");
                                        if (!string.IsNullOrWhiteSpace(report.Endometrium.FlowPattern))
                                            endoCdifPara.Append($"（{report.Endometrium.FlowPattern}）");
                                        endoCdifPara.Append("。");
                                        endoCdfiText = endoCdifPara.ToString();
                                    }
                                    else if (report.Endometrium.HasNoFlow)
                                    {
                                        endoCdfiText = "子宫内膜CDFI未见明显血流信号。";
                                    }
                                    
                                    if (!string.IsNullOrWhiteSpace(endoCdfiText))
                                    {
                                        findingsText.Append("\n");
                                        findingsText.Append(endoCdfiText);
                                    }
                                }
                            }

                            // 宫腔占位性病变
                            string? cavityText = null;
                            if (report.Cavity.HasLesion)
                            {
                                var cavityPara = new System.Text.StringBuilder();
                                cavityPara.Append("宫腔内可见占位性病变");
                                if (!string.IsNullOrWhiteSpace(report.Cavity.Location))
                                    cavityPara.Append($"，位于{report.Cavity.Location}");
                                cavityPara.Append($"，大小约{report.Cavity.Length}*{report.Cavity.APDiameter}*{report.Cavity.Width}cm，");
                                cavityPara.Append($"{(report.Cavity.IsPedunculated ? "带蒂" : "无蒂")}");
                                if (!string.IsNullOrWhiteSpace(report.Cavity.EchoType))
                                    cavityPara.Append($"，{report.Cavity.EchoType}");
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

                            // 卵巢/附件按四个部位分别输出
                            if (report.AdnexaRegions != null && report.AdnexaRegions.Any())
                            {
                                foreach (var region in report.AdnexaRegions)
                                {
                                    if (region == null) continue;
                                    var regionText = new System.Text.StringBuilder();

                                    // 正常：大小 + 囊性回声（卵泡）描述；未填写时直接“未见明显异常回声”
                                    if (region.IsNormal)
                                    {
                                        var hasSize = region.Length > 0 && region.Width > 0 && region.Height > 0;
                                        var hasCyst = region.CystCount > 0 && region.MaxCystDiameter > 0;

                                        if (!hasSize && !hasCyst)
                                        {
                                            regionText.Append($"{region.Name}未见明显异常回声。");
                                        }
                                        else
                                        {
                                            regionText.Append($"{region.Name}");
                                            if (hasSize)
                                                regionText.Append($"大小约{region.Length}*{region.Width}*{region.Height}cm");
                                            if (hasCyst)
                                                regionText.Append($"，其内见{region.CystCount}个囊性回声，较大直径约{region.MaxCystDiameter}cm");
                                            regionText.Append("，未见明显异常回声。");
                                        }
                                    }

                                    // 异常：按类目输出（同一部位可多选）
                                    if (region.IsAbnormal)
                                    {
                                        void AppendSentence(string s)
                                        {
                                            if (string.IsNullOrWhiteSpace(s)) return;
                                            if (!s.EndsWith("。")) s += "。";
                                            if (regionText.Length > 0) regionText.Append(" ");
                                            regionText.Append(s);
                                        }

                                        if (region.HasUnilocularCyst)
                                        {
                                            var echoes = new System.Collections.Generic.List<string>();
                                            if (region.UnilocularCyst.IsSimpleCyst) echoes.Add("单纯性囊肿");
                                            if (region.UnilocularCyst.IsNonSimpleCyst) echoes.Add("非单纯性囊肿");
                                            if (region.UnilocularCyst.EchoSmoothWall) echoes.Add("囊肿内壁光滑");
                                            if (region.UnilocularCyst.EchoRoughWall) echoes.Add("囊肿内壁不光滑");
                                            if (region.UnilocularCyst.EchoDenseDots) echoes.Add("内见密集细点状回声");
                                            if (region.UnilocularCyst.EchoFlocculent) echoes.Add("内见絮状回声");
                                            if (region.UnilocularCyst.EchoGrid) echoes.Add("内见网格样回声");
                                            if (region.UnilocularCyst.EchoStrongMass) echoes.Add("内见强回声团");
                                            if (region.UnilocularCyst.EchoShortLines) echoes.Add("内见短线样强回声");
                                            if (region.UnilocularCyst.EchoWeakDots) echoes.Add("内见弱点状回声");
                                            if (region.UnilocularCyst.EchoPatchy) echoes.Add("内见片状回声");
                                            if (!string.IsNullOrWhiteSpace(region.UnilocularCyst.EchoOther)) echoes.Add(region.UnilocularCyst.EchoOther);

                                            var uni = new System.Text.StringBuilder();
                                            uni.Append($"{region.Name}见一单房囊肿，大小约{region.UnilocularCyst.Length}*{region.UnilocularCyst.Width}*{region.UnilocularCyst.Height}cm");
                                            if (echoes.Any()) uni.Append($"，{string.Join("，", echoes)}");
                                            if (!string.IsNullOrWhiteSpace(region.UnilocularCyst.Boundary)) uni.Append($"，边界{region.UnilocularCyst.Boundary}");
                                            if (!string.IsNullOrWhiteSpace(region.UnilocularCyst.Shadow)) uni.Append($"，{region.UnilocularCyst.Shadow}声影");
                                            if (region.UnilocularCyst.BloodFlowScore > 0) uni.Append($"，血流评分{region.UnilocularCyst.BloodFlowScore}");
                                            AppendSentence(uni.ToString());
                                        }

                                        if (region.HasMultilocularCyst)
                                        {
                                            var echoes = new System.Collections.Generic.List<string>();
                                            if (region.MultilocularCyst.EchoSmoothWall) echoes.Add("囊肿内壁光滑");
                                            if (region.MultilocularCyst.EchoRoughWall) echoes.Add("囊肿内壁不光滑");
                                            if (region.MultilocularCyst.EchoSmoothSeptum) echoes.Add("分隔光滑");
                                            if (region.MultilocularCyst.EchoRoughSeptum) echoes.Add("分隔不光滑");
                                            if (region.MultilocularCyst.EchoGoodTransmission) echoes.Add("透声好");
                                            if (region.MultilocularCyst.EchoPoorTransmission) echoes.Add("透声差");
                                            if (region.MultilocularCyst.EchoDenseDots) echoes.Add("内见密集细点状回声");
                                            if (region.MultilocularCyst.EchoFlocculent) echoes.Add("内见絮状回声");
                                            if (region.MultilocularCyst.EchoStrongMass) echoes.Add("内见强回声团");
                                            if (region.MultilocularCyst.EchoShortLines) echoes.Add("内见短线样强回声");
                                            if (region.MultilocularCyst.EchoWeakDots) echoes.Add("内见弱点状回声");
                                            if (region.MultilocularCyst.EchoPatchy) echoes.Add("内见片状回声");
                                            if (region.MultilocularCyst.EchoRegularInnerWall) echoes.Add("内壁规则");
                                            if (region.MultilocularCyst.EchoIrregularInnerWall) echoes.Add("内壁不规则");
                                            if (region.MultilocularCyst.EchoMoreThan10Locules) echoes.Add("超过十个囊腔");
                                            if (!string.IsNullOrWhiteSpace(region.MultilocularCyst.EchoOther)) echoes.Add(region.MultilocularCyst.EchoOther);

                                            var flows = new System.Collections.Generic.List<string>();
                                            if (region.MultilocularCyst.FlowOnSeptum) flows.Add("分隔血流");
                                            if (region.MultilocularCyst.FlowOnWall) flows.Add("囊壁血流");

                                            var multi = new System.Text.StringBuilder();
                                            multi.Append($"{region.Name}见一多房囊肿，大小约{region.MultilocularCyst.Length}*{region.MultilocularCyst.Width}*{region.MultilocularCyst.Height}cm");
                                            if (echoes.Any()) multi.Append($"，{string.Join("，", echoes)}");
                                            if (!string.IsNullOrWhiteSpace(region.MultilocularCyst.Boundary)) multi.Append($"，边界{region.MultilocularCyst.Boundary}");
                                            if (!string.IsNullOrWhiteSpace(region.MultilocularCyst.Shadow)) multi.Append($"，{region.MultilocularCyst.Shadow}声影");
                                            if (region.MultilocularCyst.BloodFlowScore > 0) multi.Append($"，血流评分{region.MultilocularCyst.BloodFlowScore}");
                                            if (flows.Any()) multi.Append($"，血流分布：{string.Join("，", flows)}");
                                            AppendSentence(multi.ToString());
                                        }

                                        if (region.HasSolidCyst)
                                        {
                                            var echoes = new System.Collections.Generic.List<string>();
                                            if (region.SolidCyst.EchoSmoothWall) echoes.Add("囊性部分内壁光滑");
                                            if (region.SolidCyst.EchoRoughWall) echoes.Add("囊性部分内壁不光滑");
                                            if (region.SolidCyst.EchoSmoothSeptum) echoes.Add("分隔光滑");
                                            if (region.SolidCyst.EchoRoughSeptum) echoes.Add("分隔不光滑");
                                            if (region.SolidCyst.EchoGoodTransmission) echoes.Add("透声好");
                                            if (region.SolidCyst.EchoPoorTransmission) echoes.Add("透声差");
                                            if (region.SolidCyst.EchoDenseDots) echoes.Add("内见密集细点状回声");
                                            if (region.SolidCyst.EchoFlocculent) echoes.Add("内见絮状回声");
                                            if (region.SolidCyst.EchoGrid) echoes.Add("内见网格样回声");
                                            if (region.SolidCyst.EchoStrongMass) echoes.Add("内见强回声团");
                                            if (region.SolidCyst.EchoShortLines) echoes.Add("内见短线样强回声");
                                            if (region.SolidCyst.EchoWeakDots) echoes.Add("内见弱点状回声");
                                            if (region.SolidCyst.EchoPatchy) echoes.Add("内见片状回声");
                                            if (region.SolidCyst.EchoMoreThan10Locules) echoes.Add("超过十个囊腔");
                                            if (!string.IsNullOrWhiteSpace(region.SolidCyst.EchoOther)) echoes.Add(region.SolidCyst.EchoOther);

                                            var solid = new System.Text.StringBuilder();
                                            solid.Append($"{region.Name}见一囊实性回声肿物，大小约{region.SolidCyst.Length}*{region.SolidCyst.Width}*{region.SolidCyst.Height}cm");
                                            if (echoes.Any()) solid.Append($"，{string.Join("，", echoes)}");

                                            if (region.SolidCyst.HasPapillary)
                                            {
                                                var papEchoes = new System.Collections.Generic.List<string>();
                                                if (region.SolidCyst.PapillaryEchoLow) papEchoes.Add("低回声");
                                                if (region.SolidCyst.PapillaryEchoIso) papEchoes.Add("等回声");
                                                if (region.SolidCyst.PapillaryEchoHigh) papEchoes.Add("高回声");

                                                solid.Append($"，可见乳头{region.SolidCyst.PapillaryCount}个，最大高度约{region.SolidCyst.PapillaryHeightVal}cm");
                                                if (papEchoes.Any()) solid.Append($"，回声{string.Join("/", papEchoes)}");
                                                if (!string.IsNullOrWhiteSpace(region.SolidCyst.PapillaryContour)) solid.Append($"，轮廓{region.SolidCyst.PapillaryContour}");
                                                if (!string.IsNullOrWhiteSpace(region.SolidCyst.PapillaryShadow)) solid.Append($"，{region.SolidCyst.PapillaryShadow}声影");
                                                if (region.SolidCyst.PapillaryHasFlow && !string.IsNullOrWhiteSpace(region.SolidCyst.PapillaryFlowAmount))
                                                    solid.Append($"，CDFI乳头内{region.SolidCyst.PapillaryFlowAmount}血流信号");
                                                else if (region.SolidCyst.PapillaryHasNoFlow)
                                                    solid.Append("，CDFI乳头内未见明显血流信号");
                                            }

                                            if (region.SolidCyst.SolidLength > 0 || region.SolidCyst.SolidWidth > 0 || region.SolidCyst.SolidHeight > 0)
                                            {
                                                var solidEchoes = new System.Collections.Generic.List<string>();
                                                if (region.SolidCyst.SolidEchoLow) solidEchoes.Add("低回声");
                                                if (region.SolidCyst.SolidEchoIso) solidEchoes.Add("等回声");
                                                if (region.SolidCyst.SolidEchoHigh) solidEchoes.Add("高回声");
                                                if (region.SolidCyst.SolidEchoOther && !string.IsNullOrWhiteSpace(region.SolidCyst.SolidEchoOtherText)) solidEchoes.Add(region.SolidCyst.SolidEchoOtherText);

                                                solid.Append($"，实性成分最大者的大小约{region.SolidCyst.SolidLength}*{region.SolidCyst.SolidWidth}*{region.SolidCyst.SolidHeight}cm");
                                                if (solidEchoes.Any()) solid.Append($"，呈{string.Join("/", solidEchoes)}");
                                                if (!string.IsNullOrWhiteSpace(region.SolidCyst.SolidBoundary)) solid.Append($"，边界{region.SolidCyst.SolidBoundary}");
                                                if (!string.IsNullOrWhiteSpace(region.SolidCyst.SolidShadow)) solid.Append($"，{region.SolidCyst.SolidShadow}声影");
                                                if (region.SolidCyst.SolidHasFlow && !string.IsNullOrWhiteSpace(region.SolidCyst.SolidFlowAmount))
                                                    solid.Append($"，CDFI实性成分内{region.SolidCyst.SolidFlowAmount}血流信号");
                                                else if (region.SolidCyst.SolidHasNoFlow)
                                                    solid.Append("，CDFI实性成分内未见明显血流信号");
                                            }

                                            if (!string.IsNullOrWhiteSpace(region.SolidCyst.Boundary)) solid.Append($"，病灶边界{region.SolidCyst.Boundary}");
                                            if (region.SolidCyst.BloodFlowScore > 0) solid.Append($"，整体血流评分{region.SolidCyst.BloodFlowScore}");
                                            AppendSentence(solid.ToString());
                                        }

                                        if (region.HasSolidMass)
                                        {
                                            var mass = new System.Text.StringBuilder();
                                            mass.Append($"{region.Name}见一实性回声肿物，大小约{region.SolidMass.Length}*{region.SolidMass.Width}*{region.SolidMass.Height}cm");

                                            var massEcho = string.Empty;
                                            if (!string.IsNullOrWhiteSpace(region.SolidMass.EchoUniformity) && !string.IsNullOrWhiteSpace(region.SolidMass.EchoType))
                                                massEcho = $"{region.SolidMass.EchoUniformity}{region.SolidMass.EchoType}回声";
                                            else if (!string.IsNullOrWhiteSpace(region.SolidMass.EchoUniformity))
                                                massEcho = $"{region.SolidMass.EchoUniformity}回声";
                                            else if (!string.IsNullOrWhiteSpace(region.SolidMass.EchoType))
                                                massEcho = $"{region.SolidMass.EchoType}回声";

                                            if (!string.IsNullOrWhiteSpace(massEcho)) mass.Append($"，呈{massEcho}");
                                            if (!string.IsNullOrWhiteSpace(region.SolidMass.Boundary)) mass.Append($"，边界{region.SolidMass.Boundary}");
                                            if (!string.IsNullOrWhiteSpace(region.SolidMass.Shadow)) mass.Append($"，{region.SolidMass.Shadow}声影");
                                            if (region.SolidMass.BloodFlowScore > 0) mass.Append($"，血流评分{region.SolidMass.BloodFlowScore}");
                                            AppendSentence(mass.ToString());
                                        }
                                    }

                                    if (regionText.Length > 0)
                                    {
                                        if (ovaryPara.Length > 0) ovaryPara.Append("\n");
                                        ovaryPara.Append(regionText);
                                    }
                                }
                            }
                            // 合并积液：独立于模板选择，填写即显示（末尾单独输出）
                            string? fluidText = null;
                            if (report.HasFluid)
                            {
                                var fluids = report.FluidLocations.Where(f => f.IsSelected).Select(f => $"{f.Name}可见积液，深约{f.Depth}cm").ToList();
                                if (report.HasFluidOther && !string.IsNullOrWhiteSpace(report.FluidOtherLocation))
                                {
                                    var otherDepth = report.FluidLocations.FirstOrDefault(f => f.Name == report.FluidOtherLocation)?.Depth ?? 0;
                                    if (otherDepth > 0)
                                        fluids.Add($"{report.FluidOtherLocation}可见积液，深约{otherDepth}cm");
                                    else
                                        fluids.Add($"{report.FluidOtherLocation}可见积液");
                                }
                                if (fluids.Any())
                                    fluidText = string.Join("，", fluids) + "。";
                            }
                            // 宫腔占位性病变
                            if (!string.IsNullOrWhiteSpace(cavityText))
                            {
                                findingsText.Append("\n\n");
                                findingsText.Append(cavityText);
                            }

                            // 卵巢/附件部分（仅当勾选卵巢模板时显示）
                            if (report.IncludeOvary)
                            {
                                findingsText.Append("\n\n");
                                if (ovaryPara.Length > 0)
                                    findingsText.Append(ovaryPara);
                            }

                            // 合并积液：不属于子宫/内膜/卵巢模板，填写即显示
                            if (!string.IsNullOrWhiteSpace(fluidText))
                            {
                                findingsText.Append("\n\n");
                                findingsText.Append(fluidText);
                            }

                            x.Item().Text(findingsText.ToString()).FontSize(12).LineHeight(1.5f);

                            // O-RADS & Conclusion
                            x.Item().PaddingTop(10).LineHorizontal(1);
                            
                            x.Item().Text("超声提示:").FontSize(14).Bold();
                            
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
