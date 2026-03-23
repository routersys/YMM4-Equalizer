using Equalizer.Enums;
using Equalizer.Interfaces;
using System.Windows;

namespace Equalizer.Services;

internal sealed class ToastManager
{
    public const double ToastWidth = 340;
    public const double ToastHeight = 72;

    private const double RightMargin = 14;
    private const double BottomMargin = 14;
    private const double StackSpacing = 6;
    private const int MaxVisible = 4;

    private readonly IToastPresenter _presenter;
    private readonly List<IToastHandle> _stack = [];
    private readonly Queue<(NotificationSeverity Severity, string Message)> _overflow = new();

    internal ToastManager(IToastPresenter presenter)
    {
        _presenter = presenter;
    }

    internal void Push(NotificationSeverity severity, string message)
    {
        if (_stack.Count >= MaxVisible)
        {
            _overflow.Enqueue((severity, message));
            return;
        }

        ShowToast(severity, message);
    }

    private void ShowToast(NotificationSeverity severity, string message)
    {
        var area = SystemParameters.WorkArea;
        double left = area.Right - ToastWidth - RightMargin;
        double top = ComputeTargetTop(area);

        var handle = _presenter.Show(severity, message, left, top);
        _stack.Add(handle);
        handle.Closed += (_, _) => OnToastClosed(handle);
    }

    private double ComputeTargetTop(Rect area)
    {
        double bottom = area.Bottom - BottomMargin;
        for (int i = 0; i < _stack.Count; i++)
            bottom -= ToastHeight + StackSpacing;
        return bottom - ToastHeight;
    }

    private void OnToastClosed(IToastHandle closed)
    {
        _stack.Remove(closed);
        RePositionStack();

        if (_overflow.TryDequeue(out var item))
            ShowToast(item.Severity, item.Message);
    }

    private void RePositionStack()
    {
        var area = SystemParameters.WorkArea;
        double bottom = area.Bottom - BottomMargin;

        for (int i = 0; i < _stack.Count; i++)
        {
            var handle = _stack[i];
            double top = bottom - ToastHeight;
            handle.AnimateTop(top);
            bottom = top - StackSpacing;
        }
    }
}