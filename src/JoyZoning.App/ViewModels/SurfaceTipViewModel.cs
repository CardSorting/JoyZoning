using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;

namespace JoyZoning.App.ViewModels;

public partial class SurfaceTipViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _body = "";

    [ObservableProperty]
    private string? _learnMoreUrl;

    [ObservableProperty]
    private string _surfaceId = "";

    public void ShowForSurface(string surfaceId)
    {
        SurfaceId = surfaceId;
        if (!OnboardingCoordinator.ShouldShowCoachMark(surfaceId))
        {
            IsVisible = false;
            return;
        }

        var mark = OnboardingCoordinator.CoachMarkFor(surfaceId);
        if (mark is null)
        {
            IsVisible = false;
            return;
        }

        Title = mark.Title;
        Body = mark.Body;
        LearnMoreUrl = mark.LearnMoreUrl;
        IsVisible = true;
    }

    [RelayCommand]
    private void Dismiss()
    {
        if (!string.IsNullOrEmpty(SurfaceId))
            OnboardingCoordinator.DismissCoachMark(SurfaceId);
        IsVisible = false;
    }
}
