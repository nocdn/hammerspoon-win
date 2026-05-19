using HsWin.Core.Alerts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MediaBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace HsWin.App;

internal interface IToastView
{
    double ActualHeight { get; }

    double ActualWidth { get; }

    bool IsVisible { get; }

    double Left { get; set; }

    double Top { get; set; }

    Visual PlacementVisual { get; }

    void Close();

    void Hide();

    void Show();

    void UpdateLayout();

    void UpdateRequest(AlertRequest request);
}

internal sealed class ToastWindow : Window, IToastView
{
    private static readonly MediaBrush WhiteBrush = WpfBrushes.White;
    private static readonly MediaBrush TextBrush = WpfBrushes.Black;
    private static readonly MediaBrush ErrorBrush = CreateFrozenBrush(WpfColor.FromRgb(242, 20, 26));
    private static readonly MediaBrush SuccessBrush = CreateFrozenBrush(WpfColor.FromRgb(22, 163, 74));
    private static readonly WpfFontFamily TextFontFamily = new("Segoe UI");

    private readonly PillBorder _border;
    private readonly Ellipse _dot;
    private readonly TextBlock _text;

    public ToastWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = WpfBrushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        _dot = CreateDotSlot();
        _text = CreateText();

        var panel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(_dot);
        panel.Children.Add(_text);

        _border = new PillBorder
        {
            Background = WhiteBrush,
            Child = panel
        };
        Content = _border;
    }

    public Visual PlacementVisual => this;

    public void UpdateRequest(AlertRequest request)
    {
        _text.Text = request.Text;

        if (request.Kind is AlertKind.Normal)
        {
            _dot.Visibility = Visibility.Collapsed;
            _border.Padding = new Thickness(
                ToastStyleMetrics.NormalHorizontalPadding,
                ToastStyleMetrics.VerticalPadding,
                ToastStyleMetrics.NormalHorizontalPadding,
                ToastStyleMetrics.VerticalPadding);
            return;
        }

        _dot.Visibility = Visibility.Visible;
        _dot.Fill = request.Kind is AlertKind.Error ? ErrorBrush : SuccessBrush;
        _border.Padding = new Thickness(
            ToastStyleMetrics.DotStateLeftPadding,
            ToastStyleMetrics.VerticalPadding,
            ToastStyleMetrics.DotStateRightPadding,
            ToastStyleMetrics.VerticalPadding);
    }

    private static Ellipse CreateDotSlot()
    {
        return new()
        {
            Width = ToastStyleMetrics.DotSize,
            Height = ToastStyleMetrics.DotSize,
            Margin = new Thickness(0, 0, ToastStyleMetrics.DotTextGap, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
    }

    private static TextBlock CreateText()
    {
        return new()
        {
            FontFamily = TextFontFamily,
            FontSize = ToastStyleMetrics.TextFontSize,
            FontWeight = ToastStyleMetrics.TextFontWeight,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static MediaBrush CreateFrozenBrush(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed class PillBorder : Border
    {
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            var radius = Math.Max(0, ActualHeight / 2);
            CornerRadius = new CornerRadius(radius);
        }
    }
}
