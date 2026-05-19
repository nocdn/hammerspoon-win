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
    private Storyboard? _storyboard;

    public ToastExitAnimator(UIElement target, BlurEffect blur)
    {
        _target = target;
        _blur = blur;
    }

    public void PrepareForShow()
    {
        Cancel();
        _target.Opacity = 1;
        _blur.Radius = 0;
    }

    public void Cancel()
    {
        if (_storyboard is null)
        {
            return;
        }

        _storyboard.Stop();
        _storyboard = null;
    }

    public void Begin(Action onComplete)
    {
        ArgumentNullException.ThrowIfNull(onComplete);
        Cancel();

        var duration = TimeSpan.FromMilliseconds(ToastStyleMetrics.ExitDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };

        var opacityAnimation = new DoubleAnimation(1, 0, duration)
        {
            EasingFunction = easing,
        };
        Storyboard.SetTarget(opacityAnimation, _target);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));

        var blurAnimation = new DoubleAnimation(0, ToastStyleMetrics.ExitBlurRadius, duration)
        {
            EasingFunction = easing,
        };
        Storyboard.SetTarget(blurAnimation, _blur);
        Storyboard.SetTargetProperty(blurAnimation, new PropertyPath(BlurEffect.RadiusProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(opacityAnimation);
        storyboard.Children.Add(blurAnimation);
        storyboard.Completed += OnCompleted;

        _storyboard = storyboard;
        storyboard.Begin();

        void OnCompleted(object? sender, EventArgs e)
        {
            storyboard.Completed -= OnCompleted;
            if (!ReferenceEquals(_storyboard, storyboard))
            {
                return;
            }

            _storyboard = null;
            onComplete();
        }
    }
}
