using CommunityToolkit.Mvvm.ComponentModel;
using CodeClone.Domain;

namespace CodeClone.App.ViewModels;

/// <summary>
/// ViewModel wrapper for displaying hotspots.
/// </summary>
public partial class HotspotItem : ObservableObject
{
    [ObservableProperty]
    private Hotspot _hotspot = null!;

    public string Icon => Hotspot.Severity switch
    {
        HotspotSeverity.Severe => "\uE7BA",   // Warning icon
        HotspotSeverity.Moderate => "\uE946", // Info icon
        _ => "\uE8FD"                          // Dot icon
    };

    public Color IconColor => Hotspot.Severity switch
    {
        HotspotSeverity.Severe => Colors.Red,
        HotspotSeverity.Moderate => Colors.Orange,
        _ => Colors.Gray
    };

    public string FileName => Path.GetFileName(Hotspot.File);

    public string Summary => $"{Hotspot.DiagnosticCount} issues • {Hotspot.UncoveredLines} uncovered lines";
}
