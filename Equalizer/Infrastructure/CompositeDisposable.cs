using System.ComponentModel;

namespace Equalizer.Infrastructure;

internal sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;

    public void Add(IDisposable disposable)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _disposables.Add(disposable);
    }

    public IDisposable Subscribe(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        source.PropertyChanged += handler;
        var subscription = new ActionDisposable(() => source.PropertyChanged -= handler);
        Add(subscription);
        return subscription;
    }

    public void Clear()
    {
        foreach (var d in _disposables)
            d.Dispose();
        _disposables.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        private Action? _action = action;

        public void Dispose()
        {
            _action?.Invoke();
            _action = null;
        }
    }
}