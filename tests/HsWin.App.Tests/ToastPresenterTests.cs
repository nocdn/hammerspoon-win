using HsWin.Core.Alerts;
using System.Windows.Media;
using System.Windows.Threading;

namespace HsWin.App.Tests;

public sealed class ToastPresenterTests
{
    [Fact]
    public void ShowDoesNotCreateWindowForZeroDurationRequest()
    {
        var createdCount = 0;
        using var presenter = new ToastPresenter(
            Dispatcher.CurrentDispatcher,
            () =>
            {
                createdCount++;
                return new FakeToastView();
            },
            _ => { });

        presenter.Show(AlertRequest.Create("Hidden", AlertKind.Normal, 0));

        Assert.Equal(0, createdCount);
    }

    [Fact]
    public void ShowReusesWarmWindowWithoutHiding()
    {
        var windows = new List<FakeToastView>();
        var positionCount = 0;
        using var presenter = new ToastPresenter(
            Dispatcher.CurrentDispatcher,
            () =>
            {
                var window = new FakeToastView();
                windows.Add(window);
                return window;
            },
            _ => positionCount++);

        var first = AlertRequest.Create("First", AlertKind.Normal, 1000);
        var second = AlertRequest.Create("Second", AlertKind.Success, 1000);

        presenter.Show(first);
        presenter.Show(AlertRequest.Create("Hide", AlertKind.Normal, 0));
        presenter.Show(second);

        var window = Assert.Single(windows);
        Assert.Equal(1, window.ShowCount);
        Assert.Equal(0, window.HideCount);
        Assert.Equal(0, window.CloseCount);
        Assert.True(window.IsVisible);
        Assert.Equal(2, positionCount);
        Assert.Collection(
            window.Requests,
            request => Assert.Equal(first, request),
            request => Assert.Equal(second, request));
    }

    [Fact]
    public void PrewarmShowsWindowOffscreenAndNextToastReusesIt()
    {
        FakeToastView? window = null;
        var positionCount = 0;
        using var presenter = new ToastPresenter(
            Dispatcher.CurrentDispatcher,
            () => window = new FakeToastView(),
            _ => positionCount++);

        presenter.Prewarm();

        Assert.NotNull(window);
        Assert.Equal(1, window.ShowCount);
        Assert.True(window.IsVisible);
        Assert.True(window.Left < -1000);
        Assert.True(window.Top < -1000);
        Assert.Equal(0, positionCount);

        presenter.Show(AlertRequest.Create("Visible", AlertKind.Success, 1000));

        Assert.Equal(1, window.ShowCount);
        Assert.Equal(1, positionCount);
        Assert.Equal(2, window.Requests.Count);
    }

    [Fact]
    public void ShowUpdatesVisibleWindowWithoutRecreatingIt()
    {
        var windows = new List<FakeToastView>();
        using var presenter = new ToastPresenter(
            Dispatcher.CurrentDispatcher,
            () =>
            {
                var window = new FakeToastView();
                windows.Add(window);
                return window;
            },
            _ => { });

        presenter.Show(AlertRequest.Create("First", AlertKind.Normal, 1000));
        presenter.Show(AlertRequest.Create("Second", AlertKind.Success, 1000));

        var window = Assert.Single(windows);
        Assert.Equal(1, window.ShowCount);
        Assert.Equal(0, window.HideCount);
        Assert.Equal(0, window.CloseCount);
        Assert.Equal(2, window.Requests.Count);
    }

    [Fact]
    public void DisposeClosesCachedWindow()
    {
        FakeToastView? window = null;
        var presenter = new ToastPresenter(
            Dispatcher.CurrentDispatcher,
            () => window = new FakeToastView(),
            _ => { });

        presenter.Show(AlertRequest.Create("Visible", AlertKind.Normal, 1000));
        presenter.Dispose();

        Assert.NotNull(window);
        Assert.Equal(1, window.CloseCount);
        Assert.False(window.IsVisible);
    }

    private sealed class FakeToastView : IToastView
    {
        public List<AlertRequest> Requests { get; } = [];

        public int CloseCount { get; private set; }

        public int HideCount { get; private set; }

        public int ShowCount { get; private set; }

        public double ActualHeight { get; set; } = 40;

        public double ActualWidth { get; set; } = 120;

        public bool IsVisible { get; private set; }

        public double Left { get; set; }

        public double Top { get; set; }

        public Visual PlacementVisual => throw new NotSupportedException();

        public void Close()
        {
            IsVisible = false;
            CloseCount++;
        }

        public void Hide()
        {
            IsVisible = false;
            HideCount++;
        }

        public void Show()
        {
            IsVisible = true;
            ShowCount++;
        }

        public void UpdateLayout()
        {
        }

        public void UpdateRequest(AlertRequest request)
        {
            Requests.Add(request);
        }
    }
}
