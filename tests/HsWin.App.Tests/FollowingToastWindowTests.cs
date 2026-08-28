using HsWin.Core.Alerts;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace HsWin.App.Tests;

public sealed class FollowingToastWindowTests
{
    [Fact]
    public void EnterAndExitUseAnimatedFadeTransitions()
    {
        RunOnStaThread(() =>
        {
            var view = new FollowingToastWindow();
            try
            {
                view.UpdateRequest(AlertRequest.Create(
                    "Testing",
                    durationMs: 6000,
                    style: AlertStyle.Following));
                view.PrepareForShow();
                view.Show();
                view.UpdateLayout();

                view.BeginEnterAnimation();
                Assert.True(view.HasActiveTransitionAnimations);

                var exitCompleted = false;
                view.BeginExitAnimation(() => exitCompleted = true);
                Assert.True(view.HasActiveTransitionAnimations);
                PumpFor(TimeSpan.FromMilliseconds(
                    FollowingToastStyleMetrics.ExitDurationMs + 60));

                Assert.True(exitCompleted);
            }
            finally
            {
                view.Close();
            }
        });
    }

    [Fact]
    public void CursorFollowerUsesIndependentNativeLoopForPopup()
    {
        RunOnStaThread(() =>
        {
            var view = new FollowingToastWindow();
            using var controller = new CursorFollowingToastController();
            try
            {
                view.UpdateRequest(AlertRequest.Create(
                    "Testing",
                    durationMs: 6000,
                    style: AlertStyle.Following));
                view.PrepareForShow();
                view.Show();
                view.UpdateLayout();

                controller.Start(view);

                Assert.NotEqual(nint.Zero, view.NativeHandle);
                Assert.True(controller.IsUsingNativeLoop);
            }
            finally
            {
                controller.Stop();
                view.Close();
            }
        });
    }

    [Fact]
    public void TextOnlyPopupSizesToContentBelowSystemWindowMinimumAndShrinksAgain()
    {
        RunOnStaThread(() =>
        {
            var view = new FollowingToastWindow();
            try
            {
                view.UpdateRequest(AlertRequest.Create(
                    "Testing",
                    durationMs: 6000,
                    style: AlertStyle.Following));
                view.PrepareForShow();
                view.Left = 100;
                view.Top = 100;
                view.Show();
                view.UpdateLayout();
                var testingWidth = view.ActualWidth;

                Assert.InRange(testingWidth, 30, SystemParameters.MinimumWindowWidth - 1);
                Assert.InRange(view.ActualHeight, 15, SystemParameters.MinimumWindowHeight - 1);

                view.UpdateRequest(AlertRequest.Create(
                    "A substantially longer following toast",
                    durationMs: 6000,
                    style: AlertStyle.Following));
                view.UpdateLayout();
                var longerWidth = view.ActualWidth;

                view.UpdateRequest(AlertRequest.Create(
                    "Testing",
                    durationMs: 6000,
                    style: AlertStyle.Following));
                view.UpdateLayout();

                Assert.True(longerWidth > testingWidth);
                Assert.Equal(testingWidth, view.ActualWidth, 3);
            }
            finally
            {
                view.Close();
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
}
