using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeClone.App.ViewModels;

/// <summary>
/// Represents a file or folder in the repository tree.
/// </summary>
public partial class FileTreeItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _fullPath = "";

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private int _diagnosticCount;

    [ObservableProperty]
    private double? _coveragePercent;

    public ObservableCollection<FileTreeItem> Children { get; } = [];

    public string Icon => IsDirectory ? "\uE8B7" : "\uE8A5"; // Folder or Document icons

    public string CoverageDisplay => CoveragePercent.HasValue
        ? $"{CoveragePercent:F0}%"
        : "";

    public string DiagnosticDisplay => DiagnosticCount > 0
        ? $"({DiagnosticCount})"
        : "";
}
