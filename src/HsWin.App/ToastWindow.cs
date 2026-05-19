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
    private static readonly MediaBrush ErrorBrush = new SolidColorBrush(WpfColor.FromRgb(242, 20, 26));
    private static readonly MediaBrush SuccessBrush = new SolidColorBrush(WpfColor.FromRgb(22, 163, 74));

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
    }

    public Visual PlacementVisual => this;

    public void UpdateRequest(AlertRequest request)
    {
        Content = CreateContent(request);
    }

    private static UIElement CreateContent(AlertRequest request)
    {
        var panel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (request.Kind is not AlertKind.Normal)
        {
            panel.Children.Add(CreateDotSlot(request.Kind));
        }

        panel.Children.Add(CreateText(request.Text));

        return new PillBorder
        {
            Background = WhiteBrush,
            Padding = request.Kind is AlertKind.Normal
                ? new Thickness(
                    ToastStyleMetrics.NormalHorizontalPadding,
                    ToastStyleMetrics.VerticalPadding,
                    ToastStyleMetrics.NormalHorizontalPadding,
                    ToastStyleMetrics.VerticalPadding)
                : new Thickness(
                    ToastStyleMetrics.DotStateLeftPadding,
                    ToastStyleMetrics.VerticalPadding,
                    ToastStyleMetrics.DotStateRightPadding,
                    ToastStyleMetrics.VerticalPadding),
            Child = panel
        };
    }

    private static UIElement CreateDotSlot(AlertKind kind)
    {
        return new Ellipse
        {
            Width = ToastStyleMetrics.DotSize,
            Height = ToastStyleMetrics.DotSize,
            Fill = kind is AlertKind.Error ? ErrorBrush : SuccessBrush,
            Margin = new Thickness(0, 0, ToastStyleMetrics.DotTextGap, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static UIElement CreateText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new WpfFontFamily("Segoe UI"),
            FontSize = ToastStyleMetrics.TextFontSize,
            FontWeight = ToastStyleMetrics.TextFontWeight,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
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
