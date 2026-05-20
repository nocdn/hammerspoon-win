using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace HsWin.App.Tests;

public sealed class ToastExitAnimatorTests
{
    [Fact]
    public void PrepareForShowClearsCompletedExitAnimation()
    {
        RunOnStaThread(() =>
        {
            var blur = new BlurEffect { Radius = 0 };
            var target = new Border { Opacity = 1, Effect = blur };
            var host = CreateHost(target);
            var animator = new ToastExitAnimator(target, blur, TimeSpan.FromMilliseconds(1), 4);
            var completed = false;

            try
            {
                host.Show();
                animator.Begin(() => completed = true);
                PumpUntil(() => completed, TimeSpan.FromSeconds(2));

                Assert.InRange(target.Opacity, 0, 0.001);
                Assert.InRange(blur.Radius, 3.999, 4.001);

                animator.PrepareForShow();

                Assert.InRange(target.Opacity, 0.999, 1);
                Assert.InRange(blur.Radius, 0, 0.001);
            }
            finally
            {
                host.Close();
            }
        });
    }

    [Fact]
    public void CancelPreventsStaleExitCompletion()
    {
        RunOnStaThread(() =>
        {
            var blur = new BlurEffect { Radius = 0 };
            var target = new Border { Opacity = 1, Effect = blur };
            var host = CreateHost(target);
            var animator = new ToastExitAnimator(target, blur, TimeSpan.FromMilliseconds(50), 4);
            var completed = false;

            try
            {
                host.Show();
                animator.Begin(() => completed = true);
                animator.PrepareForShow();
                PumpFor(TimeSpan.FromMilliseconds(120));

                Assert.False(completed);
                Assert.InRange(target.Opacity, 0.999, 1);
                Assert.InRange(blur.Radius, 0, 0.001);
            }
            finally
            {
                host.Close();
            }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                thrown = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (thrown is not null)
        {
            throw thrown;
        }
    }

    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for WPF animation completion.");
            }

            PumpFor(TimeSpan.FromMilliseconds(10));
        }
    }

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };

        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static Window CreateHost(UIElement content)
    {
        return new()
        {
            Content = content,
            Width = 1,
            Height = 1,
            Left = -10_000,
            Top = -10_000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None
        };
    }
}
