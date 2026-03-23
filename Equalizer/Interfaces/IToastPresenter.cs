using Equalizer.Services;

namespace Equalizer.Interfaces;

public interface IToastPresenter
{
    IToastHandle Show(NotificationSeverity severity, string message, double left, double top);
}