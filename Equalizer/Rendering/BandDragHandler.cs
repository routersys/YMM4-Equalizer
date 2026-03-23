using Equalizer.Models;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using YukkuriMovieMaker.Commons;

namespace Equalizer.Rendering;

internal sealed class BandDragHandler
{
    private const double ThumbHalf = 6;

    private AnimationValue? _targetFreqKeyframe;
    private AnimationValue? _targetGainKeyframe;
    private double _startThumbLeft;
    private double _startThumbTop;
    private double _startKeyframeScreenX;
    private double _startKeyframeScreenY;

    public bool IsDragging { get; private set; }
    public EQBand? DraggingBand { get; private set; }

    public void Start(Thumb thumb, EQBand band, CoordinateMapper mapper, double currentTime, int totalFrames)
    {
        IsDragging = true;
        DraggingBand = band;

        int currentFrame = (int)(totalFrames * currentTime);

        _targetFreqKeyframe = ResolveKeyframe(band.Frequency, currentTime);
        _targetGainKeyframe = ResolveKeyframe(band.Gain, currentTime);

        _startThumbLeft = Canvas.GetLeft(thumb);
        _startThumbTop = Canvas.GetTop(thumb);

        _startKeyframeScreenX = mapper.FreqToX(_targetFreqKeyframe.Value) - ThumbHalf;
        _startKeyframeScreenY = mapper.GainToY(_targetGainKeyframe.Value) - ThumbHalf;
    }

    public void Update(Thumb thumb, DragDeltaEventArgs e, CoordinateMapper mapper, double canvasWidth, double canvasHeight)
    {
        if (_targetFreqKeyframe is null || _targetGainKeyframe is null) return;

        double newX = Math.Clamp(Canvas.GetLeft(thumb) + e.HorizontalChange, 0, canvasWidth - thumb.Width);
        double newY = Math.Clamp(Canvas.GetTop(thumb) + e.VerticalChange, 0, canvasHeight - thumb.Height);

        Canvas.SetLeft(thumb, newX);
        Canvas.SetTop(thumb, newY);

        double deltaX = newX - _startThumbLeft;
        double deltaY = newY - _startThumbTop;

        _targetFreqKeyframe.Value = mapper.XToFreq(_startKeyframeScreenX + deltaX + ThumbHalf);
        _targetGainKeyframe.Value = mapper.YToGain(_startKeyframeScreenY + deltaY + ThumbHalf);
    }

    public void Complete()
    {
        IsDragging = false;
        DraggingBand = null;
        _targetFreqKeyframe = null;
        _targetGainKeyframe = null;
    }

    private static AnimationValue ResolveKeyframe(Animation animation, double normalizedTime)
    {
        int count = animation.Values.Count;

        if (count <= 1) return animation.Values.First();

        if (count == 2)
            return normalizedTime < 0.5 ? animation.Values.First() : animation.Values.Last();

        int index = Math.Clamp(
            (int)Math.Round(normalizedTime * (count - 1)),
            0,
            count - 1);

        return animation.Values[index];
    }
}