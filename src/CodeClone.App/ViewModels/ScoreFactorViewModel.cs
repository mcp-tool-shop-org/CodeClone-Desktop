using CommunityToolkit.Mvvm.ComponentModel;
using CodeClone.Domain;

namespace CodeClone.App.ViewModels;

/// <summary>
/// ViewModel for displaying score factors in the explainer.
/// </summary>
public partial class ScoreFactorViewModel : ObservableObject
{
    [ObservableProperty]
    private RiskFactor _factor = null!;

    public string Name => Factor.Name;
    public string Description => Factor.Description;
    public int Weight => Factor.Weight;
    public int Value => Factor.Value;

    public double NormalizedValue => Factor.Value / 100.0;

    public int WeightedContribution => (Factor.Weight * Factor.Value) / 100;

    public Color ProgressColor => Factor.Value switch
    {
        >= 75 => Color.FromArgb("#D32F2F"),
        >= 50 => Color.FromArgb("#F57C00"),
        >= 25 => Color.FromArgb("#FBC02D"),
        _ => Color.FromArgb("#388E3C")
    };
}
