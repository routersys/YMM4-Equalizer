using System.ComponentModel.DataAnnotations;
using Equalizer.Localization;

namespace Equalizer.Enums;

public enum FilterType
{
    [Display(Name = nameof(Texts.FilterTypePeak), ResourceType = typeof(Texts))]
    Peak,

    [Display(Name = nameof(Texts.FilterTypeLowShelf), ResourceType = typeof(Texts))]
    LowShelf,

    [Display(Name = nameof(Texts.FilterTypeHighShelf), ResourceType = typeof(Texts))]
    HighShelf
}