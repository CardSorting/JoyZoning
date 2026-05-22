using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JoyZoning.App.Services;

namespace JoyZoning.App.ViewModels;

public partial class TimelineViewModel : ViewModelBase
{
    public ObservableCollection<TimelineEventViewModel> Events { get; } = new();

    [ObservableProperty]
    private string _filter = "All";

    [ObservableProperty]
    private string? _selectedPayload = "(select an event)";

    [ObservableProperty]
    private TimelineEventViewModel? _selectedEvent;

    public string[] FilterOptions { get; } = { "All", "Hermes", "DietCode", "JoyZoning", "Approvals" };

    partial void OnSelectedEventChanged(TimelineEventViewModel? value)
    {
        SelectedPayload = value?.PayloadJson is { Length: > 0 } p
            ? p
            : "(empty payload)";
    }

    public void AddEvent(JoyEventMessage evt)
    {
        if (!PassesFilter(evt)) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var row = new TimelineEventViewModel(
                evt.Id,
                evt.OccurredAt.ToLocalTime().ToString("HH:mm:ss"),
                evt.Source,
                evt.Type,
                Summarize(evt),
                evt.PayloadJson);
            Events.Insert(0, row);
            if (SelectedEvent is null)
                SelectedEvent = row;
        });
    }

    public void LoadEvents(IReadOnlyList<ControlPlaneClient.EventInfo> events)
    {
        Events.Clear();
        foreach (var evt in events.OrderByDescending(e => e.Id).Take(200))
        {
            Events.Add(new TimelineEventViewModel(
                evt.Id,
                evt.OccurredAt.ToLocalTime().ToString("HH:mm:ss"),
                evt.Source,
                evt.Type,
                evt.Type,
                evt.PayloadJson));
        }
    }

    private bool PassesFilter(JoyEventMessage evt) => Filter switch
    {
        "Hermes" => evt.Source.Contains("Hermes", StringComparison.OrdinalIgnoreCase),
        "DietCode" => evt.Source.Contains("DietCode", StringComparison.OrdinalIgnoreCase),
        "Approvals" => evt.Type.Contains("approval", StringComparison.OrdinalIgnoreCase),
        "JoyZoning" => evt.Source.Contains("JoyZoning", StringComparison.OrdinalIgnoreCase) || evt.Source == "0",
        _ => true,
    };

    private static string Summarize(JoyEventMessage evt)
    {
        if (evt.Type.Contains("tool", StringComparison.OrdinalIgnoreCase))
            return evt.Type;
        if (evt.Type.Contains("message", StringComparison.OrdinalIgnoreCase))
            return "Assistant output";
        return evt.Type;
    }
}

public record TimelineEventViewModel(long Id, string Time, string Source, string Type, string Summary, string PayloadJson);
