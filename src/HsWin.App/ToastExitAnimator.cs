using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace HsWin.App;

/// <summary>
/// Quick exit fade with a light blur. Uses ease-in so the toast accelerates away
/// (Material / web motion guidance for elements leaving the screen).
/// </summary>
internal sealed class ToastExitAnimator
{
    private readonly UIElement _target;
    private readonly BlurEffect _blur;
    private readonly TimeSpan _duration;
    private readonly double _exitBlurRadius;
    private int _generation;

    public ToastExitAnimator(UIElement target, BlurEffect blur)
        : this(
            target,
            blur,
            TimeSpan.FromMilliseconds(ToastStyleMetrics.ExitDurationMs),
            ToastStyleMetrics.ExitBlurRadius)
    {
    }

    internal ToastExitAnimator(UIElement target, BlurEffect blur, TimeSpan duration, double exitBlurRadius)
    {
        _target = target;
        _blur = blur;
        _duration = duration;
        _exitBlurRadius = exitBlurRadius;
    }

    public void PrepareForShow()
    {
        Cancel();
        _target.Opacity = 1;
        _blur.Radius = 0;
    }

    public void Cancel()
    {
        _generation++;
        ClearAnimations();
    }

    public void Begin(Action onComplete)
    {
        ArgumentNullException.ThrowIfNull(onComplete);
        Cancel();

        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var generation = ++_generation;

        var opacityAnimation = new DoubleAnimation(1, 0, _duration)
        {
            EasingFunction = easing,
        };
        opacityAnimation.Completed += OnCompleted;

        var blurAnimation = new DoubleAnimation(0, _exitBlurRadius, _duration)
        {
            EasingFunction = easing,
        };

        _target.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        _blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);

        void OnCompleted(object? sender, EventArgs e)
        {
            opacityAnimation.Completed -= OnCompleted;
            if (generation != _generation)
            {
                return;
            }

            ClearAnimations();
            _target.Opacity = 0;
            _blur.Radius = _exitBlurRadius;
            onComplete();
        }
    }

    private void ClearAnimations()
    {
        _target.BeginAnimation(UIElement.OpacityProperty, null);
        _blur.BeginAnimation(BlurEffect.RadiusProperty, null);
    }
}
