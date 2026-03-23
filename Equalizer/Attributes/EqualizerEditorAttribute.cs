using Equalizer.Views;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace Equalizer.Attributes;

internal sealed class EqualizerEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new EqualizerControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        var editor = (EqualizerControl)control;
        editor.SetBinding(EqualizerControl.ItemsSourceProperty, ItemPropertiesBinding.Create2(itemProperties));

        if (itemProperties.FirstOrDefault()?.PropertyOwner is EqualizerAudioEffect effect)
            editor.Effect = effect;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        var editor = (EqualizerControl)control;
        BindingOperations.ClearBinding(editor, EqualizerControl.ItemsSourceProperty);
        editor.Effect = null;
    }
}