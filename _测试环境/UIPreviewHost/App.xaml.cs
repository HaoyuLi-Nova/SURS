using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SURS.App.Controls;

namespace UIPreviewHost;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var log = @"D:\Projects\SURS\_测试环境\UIPreviewHost\运行日志.txt";
        try
        {
            var panel = new PreviewPanel
            {
                DataContext = new FakeVm(),
                Width = 1280,
                Height = 900
            };
            panel.Measure(new Size(1280, 900));
            panel.Arrange(new Rect(0, 0, 1280, 900));
            panel.UpdateLayout();

            var rtb = new RenderTargetBitmap(1280, 900, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(panel);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            var outPath = @"D:\Projects\SURS\_测试环境\分发模拟-医院TEST\截图-报告编辑-纯渲染.png";
            using (var fs = File.Create(outPath)) enc.Save(fs);
            File.WriteAllText(log, "OK: " + outPath);
        }
        catch (Exception ex)
        {
            File.WriteAllText(log, "ERR: " + ex);
        }
        finally
        {
            Shutdown(0);
        }
    }
}
