using System.ComponentModel.DataAnnotations;
using Equalizer.Localization;

namespace Equalizer.Enums;

public enum StereoMode
{
    [Display(Name = nameof(Texts.StereoModeStereo), ResourceType = typeof(Texts))]
    Stereo,

    [Display(Name = nameof(Texts.StereoModeLeft), ResourceType = typeof(Texts))]
    Left,

    [Display(Name = nameof(Texts.StereoModeRight), ResourceType = typeof(Texts))]
    Right
}