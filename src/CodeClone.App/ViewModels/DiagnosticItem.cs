using CommunityToolkit.Mvvm.ComponentModel;
using CodeClone.Domain;

namespace CodeClone.App.ViewModels;

/// <summary>
/// ViewModel wrapper for displaying diagnostics.
/// </summary>
public partial class DiagnosticItem : ObservableObject
{
    [ObservableProperty]
    private Diagnostic _diagnostic = null!;

    public string Icon => Diagnostic.Severity switch
    {
        DiagnosticSeverity.Error => "\uE783",   // Error icon
        DiagnosticSeverity.Warning => "\uE7BA", // Warning icon
        _ => "\uE946"                            // Info icon
    };

    public Color IconColor => Diagnostic.Severity switch
    {
        DiagnosticSeverity.Error => Colors.Red,
        DiagnosticSeverity.Warning => Colors.Orange,
        _ => Colors.DodgerBlue
    };

    public string Location => Diagnostic.File is not null
        ? Diagnostic.Line.HasValue
            ? $"{Path.GetFileName(Diagnostic.File)}:{Diagnostic.Line}"
            : Path.GetFileName(Diagnostic.File)
        : "";

    public string Summary => $"{Diagnostic.Code}: {Diagnostic.Message}";
}
