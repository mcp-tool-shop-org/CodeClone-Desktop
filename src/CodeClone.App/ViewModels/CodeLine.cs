using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeClone.App.ViewModels;

/// <summary>
/// Represents a single line of code with coverage status.
/// </summary>
public partial class CodeLine : ObservableObject
{
    [ObservableProperty]
    private int _lineNumber;

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private CoverageStatus _status = CoverageStatus.None;

    [ObservableProperty]
    private bool _hasDiagnostic;

    public Color BackgroundColor => Status switch
    {
        CoverageStatus.Covered => Color.FromArgb("#1A4CAF50"),    // Light green
        CoverageStatus.Uncovered => Color.FromArgb("#1AF44336"), // Light red
        _ => Colors.Transparent
    };

    public Color LineNumberColor => Status switch
    {
        CoverageStatus.Covered => Color.FromArgb("#4CAF50"),
        CoverageStatus.Uncovered => Color.FromArgb("#F44336"),
        _ => Colors.Gray
    };
}

public enum CoverageStatus
{
    None,
    Covered,
    Uncovered
}
