using Equalizer.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Equalizer.Views;

internal sealed class ToastWindow : Window, IToastHandle
{
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SlideInDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan SlideOutDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(220);
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction EaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };

    private readonly DispatcherTimer _dismissTimer;
    private double _targetLeft;
    private bool _isDismissing;

    internal ToastWindow(NotificationSeverity severity, string message)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = ToastManager.ToastWidth;
        Height = ToastManager.ToastHeight;

        Content = BuildContent(severity, message);

        _dismissTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = AutoDismissDelay
        };
        _dismissTimer.Tick += (_, _) => Dismiss();

        MouseEnter += (_, _) => _dismissTimer.Stop();
        MouseLeave += (_, _) =>
        {
            if (!_isDismissing) _dismissTimer.Start();
        };
    }

    public void ShowAt(double targetLeft, double targetTop)
    {
        _targetLeft = targetLeft;
        Left = SystemParameters.WorkArea.Right + 20;
        Top = targetTop;

        Show();

        var anim = new DoubleAnimation(targetLeft, SlideInDuration) { EasingFunction = EaseOut };
        BeginAnimation(LeftProperty, anim);

        _dismissTimer.Start();
    }

    public void AnimateTop(double targetTop)
    {
        var anim = new DoubleAnimation(Top, targetTop, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = EaseOut
        };
        BeginAnimation(TopProperty, anim);
    }

    private void Dismiss()
    {
        if (_isDismissing) return;
        _isDismissing = true;
        _dismissTimer.Stop();

        double exitLeft = SystemParameters.WorkArea.Right + 20;
        var slideOut = new DoubleAnimation(_targetLeft, exitLeft, SlideOutDuration) { EasingFunction = EaseIn };
        var fadeOut = new DoubleAnimation(1, 0, FadeOutDuration);

        fadeOut.Completed += (_, _) => Close();

        BeginAnimation(LeftProperty, slideOut);
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private UIElement BuildContent(NotificationSeverity severity, string message)
    {
        var (bg, fg, iconData) = ResolveTheme(severity);

        var border = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14, 10, 10, 10),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.28,
                Direction = 270
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Path
        {
            Data = Geometry.Parse(iconData),
            Fill = fg,
            Stretch = Stretch.Uniform,
            Width = 20,
            Height = 20,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);

        var textBlock = new TextBlock
        {
            Text = message,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            LineHeight = 18,
            MaxHeight = 50,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = message.Length > 80 ? new ToolTip { Content = message } : null
        };
        Grid.SetColumn(textBlock, 1);

        var closeButton = BuildCloseButton(fg);
        Grid.SetColumn(closeButton, 2);

        grid.Children.Add(icon);
        grid.Children.Add(textBlock);
        grid.Children.Add(closeButton);

        border.Child = grid;
        return border;
    }

    private Button BuildCloseButton(SolidColorBrush fg)
    {
        var button = new Button
        {
            Content = "✕",
            Foreground = fg,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            Padding = new Thickness(6, 2, 2, 2),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top,
            Opacity = 0.75
        };

        button.MouseEnter += (_, _) => button.Opacity = 1.0;
        button.MouseLeave += (_, _) => button.Opacity = 0.75;
        button.Click += (_, _) => Dismiss();

        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(ContentPresenter));
        template.VisualTree = factory;
        button.Template = template;

        return button;
    }

    private static (SolidColorBrush bg, SolidColorBrush fg, string icon) ResolveTheme(NotificationSeverity severity) =>
        severity switch
        {
            NotificationSeverity.Error => (
                Frozen(Color.FromRgb(185, 28, 28)),
                Frozen(Colors.White),
                "M13,13H11V7H13M13,17H11V15H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"),
            NotificationSeverity.Warning => (
                Frozen(Color.FromRgb(180, 83, 9)),
                Frozen(Colors.White),
                "M13,14H11V10H13M13,18H11V16H13M1,21H23L12,2L1,21Z"),
            _ => (
                Frozen(Color.FromRgb(29, 78, 216)),
                Frozen(Colors.White),
                "M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 12,2Z")
        };

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}