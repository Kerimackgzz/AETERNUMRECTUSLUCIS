namespace AETKAHVE.Web.Models;

public sealed class DashboardSummaryViewModel
{
    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<StatusPresentationViewModel> Statuses { get; init; } = [];
}

public sealed class StatusPresentationViewModel
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Kind { get; init; } = "neutral";
}

