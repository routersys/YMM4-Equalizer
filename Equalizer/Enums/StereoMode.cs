using System.ComponentModel.DataAnnotations;

namespace Equalizer.Enums;

public enum StereoMode
{
    [Display(Name = "ステレオ")]
    Stereo,

    [Display(Name = "L (左)")]
    Left,

    [Display(Name = "R (右)")]
    Right
}