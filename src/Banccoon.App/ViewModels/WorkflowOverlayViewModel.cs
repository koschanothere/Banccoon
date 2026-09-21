using System.Windows.Input;

namespace Banccoon.App.ViewModels;

public sealed class WorkflowOverlayViewModel : ViewModelBase
{
    private bool isOpen;
    private bool isBusy;
    private bool canDismiss = true;
    private string title = string.Empty;
    private string message = string.Empty;
    private string bodyText = string.Empty;
    private string stepTitle = string.Empty;
    private string errorMessage = string.Empty;
    private string primaryActionText = "Next";
    private string secondaryActionText = "Back";
    private int stepIndex = 1;
    private int stepCount = 1;

    public WorkflowOverlayViewModel()
    {
        CloseCommand = new RelayCommand(Close);
        CancelCommand = new RelayCommand(Cancel);
        NextStepCommand = new RelayCommand(MoveNext);
        PreviousStepCommand = new RelayCommand(MovePrevious);
    }

    public ICommand CloseCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand NextStepCommand { get; }

    public ICommand PreviousStepCommand { get; }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(CanClose));
                OnPropertyChanged(nameof(CanUseActions));
                OnPropertyChanged(nameof(CanMovePrevious));
                OnPropertyChanged(nameof(CanMoveNext));
                OnPropertyChanged(nameof(HasContentSurface));
            }
        }
    }

    public bool IsReady => !IsBusy;

    public bool CanClose => CanDismiss && !IsBusy;

    public bool CanUseActions => !IsBusy;

    public bool CanDismiss
    {
        get => canDismiss;
        set
        {
            if (SetProperty(ref canDismiss, value))
            {
                OnPropertyChanged(nameof(CanClose));
            }
        }
    }

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public string Message
    {
        get => message;
        private set
        {
            if (SetProperty(ref message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public string BodyText
    {
        get => bodyText;
        set
        {
            if (SetProperty(ref bodyText, value))
            {
                OnPropertyChanged(nameof(HasBodyText));
            }
        }
    }

    public bool HasBodyText => !string.IsNullOrWhiteSpace(BodyText);

    public bool HasContentSurface => HasBodyText || HasStepTitle || HasError || IsBusy;

    public string StepTitle
    {
        get => stepTitle;
        set
        {
            if (SetProperty(ref stepTitle, value))
            {
                OnPropertyChanged(nameof(HasStepTitle));
                OnPropertyChanged(nameof(HasContentSurface));
            }
        }
    }

    public bool HasStepTitle => !string.IsNullOrWhiteSpace(StepTitle);

    public string ErrorMessage
    {
        get => errorMessage;
        set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(HasContentSurface));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public int StepIndex
    {
        get => stepIndex;
        set
        {
            var normalized = Math.Max(1, value);
            if (SetProperty(ref stepIndex, normalized))
            {
                OnPropertyChanged(nameof(StepProgressText));
                OnPropertyChanged(nameof(CanMovePrevious));
                OnPropertyChanged(nameof(CanMoveNext));
            }
        }
    }

    public int StepCount
    {
        get => stepCount;
        set
        {
            var normalized = Math.Max(1, value);
            if (SetProperty(ref stepCount, normalized))
            {
                if (StepIndex > normalized)
                {
                    StepIndex = normalized;
                }

                OnPropertyChanged(nameof(HasStepProgress));
                OnPropertyChanged(nameof(StepProgressText));
                OnPropertyChanged(nameof(CanMoveNext));
            }
        }
    }

    public bool HasStepProgress => StepCount > 1;

    public string StepProgressText => $"Step {StepIndex} of {StepCount}";

    public bool CanMovePrevious => !IsBusy && StepIndex > 1;

    public bool CanMoveNext => !IsBusy && StepIndex < StepCount;

    public string PrimaryActionText
    {
        get => primaryActionText;
        set => SetProperty(ref primaryActionText, value);
    }

    public string SecondaryActionText
    {
        get => secondaryActionText;
        set => SetProperty(ref secondaryActionText, value);
    }

    public void Open(
        string title,
        string message,
        string bodyText = "",
        string stepTitle = "",
        int stepIndex = 1,
        int stepCount = 1,
        bool canDismiss = true)
    {
        Title = title;
        Message = message;
        BodyText = bodyText;
        StepTitle = stepTitle;
        StepCount = stepCount;
        StepIndex = stepIndex;
        CanDismiss = canDismiss;
        ErrorMessage = string.Empty;
        IsBusy = false;
        IsOpen = true;
    }

    public void Close()
    {
        if (!CanDismiss || IsBusy)
        {
            return;
        }

        Reset();
    }

    public void Cancel()
    {
        Close();
    }

    public void MoveNext()
    {
        if (CanMoveNext)
        {
            StepIndex++;
        }
    }

    public void MovePrevious()
    {
        if (CanMovePrevious)
        {
            StepIndex--;
        }
    }

    private void Reset()
    {
        IsOpen = false;
        IsBusy = false;
        CanDismiss = true;
        Title = string.Empty;
        Message = string.Empty;
        BodyText = string.Empty;
        StepTitle = string.Empty;
        ErrorMessage = string.Empty;
        StepIndex = 1;
        StepCount = 1;
        PrimaryActionText = "Next";
        SecondaryActionText = "Back";
    }
}
