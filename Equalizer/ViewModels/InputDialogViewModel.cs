namespace Equalizer.ViewModels;

public sealed class InputDialogViewModel(string message, string title, string defaultText = "") : ViewModelBase
{
    private string _title = title;
    private string _message = message;
    private string _inputText = defaultText;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }
}