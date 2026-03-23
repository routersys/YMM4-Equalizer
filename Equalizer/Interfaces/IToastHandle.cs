namespace Equalizer.Interfaces;

public interface IToastHandle
{
    event EventHandler Closed;
    void AnimateTop(double top);
}