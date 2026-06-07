using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Appearance;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.ViewModels;

public sealed class RecurrenceEditorViewModel : ViewModelBase
{
    private readonly IRecurrenceDescriptionService recurrenceDescriptionService;
    private readonly IRecurrenceSyntaxService recurrenceSyntaxService;
    private readonly IRecurrenceValidationService recurrenceValidationService;
    private RecurrenceFrequency frequency = RecurrenceFrequency.Weekly;
    private int interval = 1;
    private DayOfWeek dayOfWeek;
    private int dayOfMonth;
    private MonthlyRecurrenceMode monthlyMode = MonthlyRecurrenceMode.DayOfMonth;
    private DateTime startDate;
    private DateTime endDate;
    private bool hasEndDate;
    private bool showPowerUserFeatures = UiPreferences.Default.ShowPowerUserFeatures;
    private bool isTechnicalSyntaxExpanded;
    private bool isUpdatingTechnicalSyntax;
    private string technicalSyntax = string.Empty;

    public RecurrenceEditorViewModel(
        IDateProvider dateProvider,
        IRecurrenceDescriptionService recurrenceDescriptionService,
        IRecurrenceSyntaxService recurrenceSyntaxService,
        IRecurrenceValidationService recurrenceValidationService)
    {
        this.recurrenceDescriptionService = recurrenceDescriptionService;
        this.recurrenceSyntaxService = recurrenceSyntaxService;
        this.recurrenceValidationService = recurrenceValidationService;

        SyntaxExamples = recurrenceSyntaxService.GetExamples();
        ApplyTechnicalSyntaxCommand = new RelayCommand(ApplyTechnicalSyntax);

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

    public IReadOnlyList<RecurrenceSyntaxExample> SyntaxExamples { get; }

    public ICommand ApplyTechnicalSyntaxCommand { get; }

    public RecurrenceFrequency Frequency
    {
        get => frequency;
        set
        {
            if (SetProperty(ref frequency, value))
            {
                OnPropertyChanged(nameof(IsWeekly));
                OnPropertyChanged(nameof(IsMonthly));
                OnPropertyChanged(nameof(UsesDayOfMonth));
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

    public bool ShowPowerUserFeatures
    {
        get => showPowerUserFeatures;
        set => SetProperty(ref showPowerUserFeatures, value);
    }

    public bool IsTechnicalSyntaxExpanded
    {
        get => isTechnicalSyntaxExpanded;
        set => SetProperty(ref isTechnicalSyntaxExpanded, value);
    }

    public string TechnicalSyntax
    {
        get => technicalSyntax;
        set => SetProperty(ref technicalSyntax, value);
    }

    public bool IsWeekly => Frequency == RecurrenceFrequency.Weekly;

    public bool IsMonthly => Frequency == RecurrenceFrequency.Monthly;

    public bool UsesDayOfMonth => IsMonthly && MonthlyMode == MonthlyRecurrenceMode.DayOfMonth;

    public string Description { get; private set; } = string.Empty;

    public string ValidationMessage { get; private set; } = string.Empty;

    public string TechnicalSyntaxMessage { get; private set; } = string.Empty;

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

    private void ApplyTechnicalSyntax()
    {
        var parseResult = recurrenceSyntaxService.TryParse(TechnicalSyntax);
        if (!parseResult.IsValid || parseResult.Rule is null)
        {
            TechnicalSyntaxMessage = parseResult.Errors.Count == 0
                ? "Syntax could not be parsed."
                : parseResult.Errors[0];
            OnPropertyChanged(nameof(TechnicalSyntaxMessage));
            return;
        }

        ApplyRule(parseResult.Rule);
        TechnicalSyntaxMessage = "Technical syntax applied.";
        OnPropertyChanged(nameof(TechnicalSyntaxMessage));
    }

    private void ApplyRule(RecurrenceRule rule)
    {
        frequency = rule.Frequency;
        interval = rule.Interval;
        dayOfWeek = rule.DayOfWeek ?? rule.StartDate.DayOfWeek;
        dayOfMonth = rule.DayOfMonth ?? rule.StartDate.Day;
        monthlyMode = rule.MonthlyMode;
        startDate = rule.StartDate.ToDateTime(TimeOnly.MinValue);
        hasEndDate = rule.EndDate.HasValue;
        endDate = (rule.EndDate ?? rule.StartDate.AddMonths(3)).ToDateTime(TimeOnly.MinValue);

        OnPropertyChanged(nameof(Frequency));
        OnPropertyChanged(nameof(Interval));
        OnPropertyChanged(nameof(DayOfWeek));
        OnPropertyChanged(nameof(DayOfMonth));
        OnPropertyChanged(nameof(MonthlyMode));
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(HasEndDate));
        OnPropertyChanged(nameof(EndDate));
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(UsesDayOfMonth));

        UpdateDerivedState();
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

        if (validationResult.IsValid && !isUpdatingTechnicalSyntax)
        {
            isUpdatingTechnicalSyntax = true;
            TechnicalSyntax = recurrenceSyntaxService.Format(rule);
            isUpdatingTechnicalSyntax = false;
        }

        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(Description));
    }
}
