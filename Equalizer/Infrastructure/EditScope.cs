namespace Equalizer.Infrastructure;

public sealed class EditScope : IDisposable
{
    private Action? _endAction;

    private EditScope(Action beginAction, Action endAction)
    {
        _endAction = endAction;
        beginAction();
    }

    public static EditScope Begin(Action beginAction, Action endAction) =>
        new(beginAction, endAction);

    public void Dispose()
    {
        _endAction?.Invoke();
        _endAction = null;
    }
}