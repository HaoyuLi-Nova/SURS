using System;
using System.Windows.Input;

namespace UIPreviewHost;

public sealed class FakeReport
{
    public string EditedFindingsText { get; set; } =
        "子宫前位，大小约 57×48×42mm，形态规则，轮廓清晰。\r\n子宫内膜厚约 6mm，回声均匀，宫腔线居中。\r\n双侧附件区未见明显异常包块回声，未见明显盆腔积液。";
    public string EditedImpressionText { get; set; } =
        "超声提示：子宫及双侧附件未见明显异常。建议定期随访，如有不适随诊。";
}

public sealed class FakeCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) { }
}

public sealed class FakeVm
{
    public bool IsPreviewVisible => true;
    public bool IsUpdatingPreview => false;
    public double PreviewZoomPercent => 100;
    public double PreviewZoom => 1.0;
    public object? PreviewImage => null;
    public FakeReport Report { get; } = new();
    public ICommand ZoomInPreviewCommand { get; } = new FakeCommand();
    public ICommand ZoomOutPreviewCommand { get; } = new FakeCommand();
    public ICommand ResetZoomPreviewCommand { get; } = new FakeCommand();
    public ICommand TogglePreviewCommand { get; } = new FakeCommand();
    public ICommand FillNarrativeFromStructureCommand { get; } = new FakeCommand();
    public ICommand ClearManualNarrativeCommand { get; } = new FakeCommand();
}
