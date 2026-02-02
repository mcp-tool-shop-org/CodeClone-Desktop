using CommunityToolkit.Mvvm.ComponentModel;
using CodeClone.Domain;

namespace CodeClone.App.ViewModels;

/// <summary>
/// ViewModel for actionable recommendations.
/// </summary>
public partial class ActionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private ActionItem _item = null!;

    [ObservableProperty]
    private bool _isCompleted;

    public string FileName => Path.GetFileName(Item.File);

    public string PriorityIcon => Item.Priority switch
    {
        ActionPriority.Critical => "🔴",
        ActionPriority.High => "🟠",
        ActionPriority.Medium => "🟡",
        _ => "🟢"
    };

    public string PriorityText => Item.Priority switch
    {
        ActionPriority.Critical => "Critical",
        ActionPriority.High => "High",
        ActionPriority.Medium => "Medium",
        _ => "Low"
    };

    public Color PriorityColor => Item.Priority switch
    {
        ActionPriority.Critical => Color.FromArgb("#D32F2F"),
        ActionPriority.High => Color.FromArgb("#F57C00"),
        ActionPriority.Medium => Color.FromArgb("#FBC02D"),
        _ => Color.FromArgb("#388E3C")
    };

    public string ImpactText => $"Impact: {Item.ImpactScore}";
}
