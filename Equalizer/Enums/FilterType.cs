using System.ComponentModel.DataAnnotations;

namespace Equalizer.Enums;

public enum FilterType
{
    [Display(Name = "ピーク")]
    Peak,

    [Display(Name = "ローシェルフ")]
    LowShelf,

    [Display(Name = "ハイシェルフ")]
    HighShelf
}