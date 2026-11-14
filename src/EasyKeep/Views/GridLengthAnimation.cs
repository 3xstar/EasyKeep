using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EasyKeep.Views;

/// <summary>
/// Анимация ширины колонки Grid: в WPF нет встроенной анимации GridLength.
/// Интерполирует абсолютные (Pixel) значения с поддержкой функции плавности.
/// </summary>
public class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(GridLength), typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(GridLength), typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
        nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
    {
        double progress = clock.CurrentProgress ?? 0;
        if (EasingFunction is not null)
            progress = EasingFunction.Ease(progress);

        double from = From.IsAbsolute ? From.Value : ((GridLength)defaultOriginValue).Value;
        double to = To.IsAbsolute ? To.Value : ((GridLength)defaultDestinationValue).Value;
        return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
    }
}
