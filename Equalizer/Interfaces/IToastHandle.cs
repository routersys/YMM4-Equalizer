namespace Equalizer.Services;

public interface IToastHandle
{
    event EventHandler Closed;
    void AnimateTop(double top);
}