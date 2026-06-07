using Banccoon.Core.Abstractions;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.ViewModels;

public sealed class RecurrenceEditorViewModel : ViewModelBase
{
    private readonly IRecurrenceDescriptionService recurrenceDescriptionService;
    private readonly IRecurrenceValidationService recurrenceValidationService;
    private RecurrenceFrequency frequency = RecurrenceFrequency.Weekly;
    private int interval = 1;
    private DayOfWeek dayOfWeek;
    private int dayOfMonth;
    private MonthlyRecurrenceMode monthlyMode = MonthlyRecurrenceMode.DayOfMonth;
    private DateTime startDate;
    private DateTime endDate;
    private bool hasEndDate;

    public RecurrenceEditorViewModel(
        IDateProvider dateProvider,
        IRecurrenceDescriptionService recurrenceDescriptionService,
        IRecurrenceValidationService recurrenceValidationService)
    {
        this.recurrenceDescriptionService = recurrenceDescriptionService;
        this.recurrenceValidationService = recurrenceValidationService;

        var today = dateProvider.Today;
        dayOfWeek = today.DayOfWeek;
        dayOfMonth = today.Day;
        startDate = today.ToDateTime(TimeOnly.MinValue);
        endDate = today.AddMonths(3).ToDateTime(TimeOnly.MinValue);

        UpdateDerivedState();
    }

    public IReadOnlyList<RecurrenceFrequency> Frequencies { get; } = Enum.GetValues<RecurrenceFrequency>();

    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; } = Enum.GetValues<DayOfWeek>();

    public IReadOnlyList<MonthlyRecurrenceMode> MonthlyModes { get; } = Enum.GetValues<MonthlyRecurrenceMode>();

    public RecurrenceFrequency Frequency
    {
        get => frequency;
        set
        {
            if (SetProperty(ref frequency, value))
            {
                OnPropertyChanged(nameof(IsWeekly));
                OnPropertyChanged(nameof(IsMonthly));
                UpdateDerivedState();
            }
        }
    }

    public int Interval
    {
        get => interval;
        set
        {
            if (SetProperty(ref interval, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public DayOfWeek DayOfWeek
    {
        get => dayOfWeek;
        set
        {
            if (SetProperty(ref dayOfWeek, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public int DayOfMonth
    {
        get => dayOfMonth;
        set
        {
            if (SetProperty(ref dayOfMonth, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public MonthlyRecurrenceMode MonthlyMode
    {
        get => monthlyMode;
        set
        {
            if (SetProperty(ref monthlyMode, value))
            {
                OnPropertyChanged(nameof(UsesDayOfMonth));
                UpdateDerivedState();
            }
        }
    }

    public DateTime StartDate
    {
        get => startDate;
        set
        {
            if (SetProperty(ref startDate, value.Date))
            {
                UpdateDerivedState();
            }
        }
    }

    public DateTime EndDate
    {
        get => endDate;
        set
        {
            if (SetProperty(ref endDate, value.Date))
            {
                UpdateDerivedState();
            }
        }
    }

    public bool HasEndDate
    {
        get => hasEndDate;
        set
        {
            if (SetProperty(ref hasEndDate, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public bool IsWeekly => Frequency == RecurrenceFrequency.Weekly;

    public bool IsMonthly => Frequency == RecurrenceFrequency.Monthly;

    public bool UsesDayOfMonth => IsMonthly && MonthlyMode == MonthlyRecurrenceMode.DayOfMonth;

    public string Description { get; private set; } = string.Empty;

    public string ValidationMessage { get; private set; } = string.Empty;

    public bool IsValid { get; private set; }

    public RecurrenceRule BuildRule()
    {
        return new RecurrenceRule(
            Frequency,
            Interval,
            DateOnly.FromDateTime(StartDate),
            HasEndDate ? DateOnly.FromDateTime(EndDate) : null,
            Frequency == RecurrenceFrequency.Weekly ? DayOfWeek : null,
            UsesDayOfMonth ? DayOfMonth : null,
            MonthlyMode);
    }

    private void UpdateDerivedState()
    {
        var rule = BuildRule();
        var validationResult = recurrenceValidationService.Validate(rule);

        IsValid = validationResult.IsValid;
        ValidationMessage = validationResult.IsValid
            ? string.Empty
            : validationResult.Errors[0];
        Description = validationResult.IsValid
            ? recurrenceDescriptionService.Describe(rule)
            : "Invalid recurrence";

        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(Description));
    }
}
