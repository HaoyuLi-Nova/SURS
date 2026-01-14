using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SURS.App.Models;
using System.Linq;

namespace SURS.App.Services
{
    public class PdfService
    {
        public void GeneratePdf(SursReport report, string filePath)
        {
            // QuestPDF License configuration (Community is free for individuals/small companies)
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Microsoft YaHei"));

                    page.Header()
                        .Text("SURS 超声检查报告")
                        .SemiBold().FontSize(20).AlignCenter();

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(10);

                            // Ovaries
                            x.Item().Text("1. 卵巢所见").Bold().FontSize(14);
                            x.Item().Text($"左侧卵巢: {report.LeftOvary.Length} x {report.LeftOvary.Width} x {report.LeftOvary.Height} cm, 囊肿数: {report.LeftOvary.CystCount}, 最大径: {report.LeftOvary.MaxCystDiameter} cm");
                            x.Item().Text($"右侧卵巢: {report.RightOvary.Length} x {report.RightOvary.Width} x {report.RightOvary.Height} cm, 囊肿数: {report.RightOvary.CystCount}, 最大径: {report.RightOvary.MaxCystDiameter} cm");

                            // Unilocular Cyst
                            if (report.HasUnilocularCyst)
                            {
                                x.Item().Text("2. 单房囊肿").Bold().FontSize(14);
                                x.Item().Text($"大小: {report.UnilocularCyst.Length} x {report.UnilocularCyst.Width} x {report.UnilocularCyst.Height} cm");
                                x.Item().Text($"位置: {report.UnilocularCyst.Location}");
                                x.Item().Text($"边界: {report.UnilocularCyst.Boundary}, 声影: {report.UnilocularCyst.Shadow}, 血流评分: {report.UnilocularCyst.BloodFlowScore}");
                                // Add more details as needed
                            }

                            // Multilocular Cyst
                            if (report.HasMultilocularCyst)
                            {
                                x.Item().Text("3. 多房囊肿").Bold().FontSize(14);
                                x.Item().Text($"大小: {report.MultilocularCyst.Length} x {report.MultilocularCyst.Width} x {report.MultilocularCyst.Height} cm");
                                x.Item().Text($"位置: {report.MultilocularCyst.Location}");
                                x.Item().Text($"血流评分: {report.MultilocularCyst.BloodFlowScore}");
                            }

                            // Solid Cyst
                            if (report.HasSolidCyst)
                            {
                                x.Item().Text("4. 实性成分囊肿").Bold().FontSize(14);
                                x.Item().Text($"大小: {report.SolidCyst.Length} x {report.SolidCyst.Width} x {report.SolidCyst.Height} cm");
                                if (report.SolidCyst.HasPapillary)
                                {
                                    x.Item().Text($"含乳头状突起: {report.SolidCyst.PapillaryCount} 个, 最大高度: {report.SolidCyst.PapillaryHeightVal} cm");
                                }
                            }

                            // Solid Mass
                            if (report.HasSolidMass)
                            {
                                x.Item().Text("5. 实性肿物").Bold().FontSize(14);
                                x.Item().Text($"大小: {report.SolidMass.Length} x {report.SolidMass.Width} x {report.SolidMass.Height} cm");
                                x.Item().Text($"回声: {report.SolidMass.EchoType}, {report.SolidMass.EchoUniformity}");
                            }

                            // Fluid
                            if (report.HasFluid)
                            {
                                x.Item().Text("6. 积液").Bold().FontSize(14);
                                foreach(var fluid in report.FluidLocations.Where(f => f.IsSelected))
                                {
                                    x.Item().Text($"- {fluid.Name}: 深度 {fluid.Depth} cm");
                                }
                            }

                            // O-RADS
                            x.Item().PaddingTop(10).LineHorizontal(1);
                            x.Item().PaddingTop(5).Text($"O-RADS 分级: {report.ORadsScore}").Bold().FontSize(16).FontColor(Colors.Red.Medium);
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(filePath);
        }
    }
}
