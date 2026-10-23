using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using NOTM.Core.Display;

namespace NOTM.UI;

public partial class MonitorVisualMap : UserControl
{
    public static readonly DependencyProperty DisplaysSourceProperty =
        DependencyProperty.Register(
            nameof(DisplaysSource),
            typeof(IEnumerable<DisplayInfo>),
            typeof(MonitorVisualMap),
            new PropertyMetadata(null, OnDisplaysSourceChanged));

    public static readonly DependencyProperty SelectedDisplayProperty =
        DependencyProperty.Register(
            nameof(SelectedDisplay),
            typeof(DisplayInfo),
            typeof(MonitorVisualMap),
            new PropertyMetadata(null, OnSelectedDisplayChanged));

    public IEnumerable<DisplayInfo>? DisplaysSource
    {
        get => (IEnumerable<DisplayInfo>?)GetValue(DisplaysSourceProperty);
        set => SetValue(DisplaysSourceProperty, value);
    }

    public DisplayInfo? SelectedDisplay
    {
        get => (DisplayInfo?)GetValue(SelectedDisplayProperty);
        set => SetValue(SelectedDisplayProperty, value);
    }

    public event Action<DisplayInfo>? MonitorSelected;

    public MonitorVisualMap()
    {
        InitializeComponent();
        SizeChanged += (s, e) => Redraw();
    }

    private static void OnDisplaysSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonitorVisualMap map)
        {
            if (e.OldValue is INotifyCollectionChanged oldCol)
            {
                oldCol.CollectionChanged -= map.OnCollectionChanged;
            }
            if (e.NewValue is INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += map.OnCollectionChanged;
            }

            map.HookItemPropertyChanged(e.OldValue as IEnumerable<DisplayInfo>, false);
            map.HookItemPropertyChanged(e.NewValue as IEnumerable<DisplayInfo>, true);

            map.Redraw();
        }
    }

    private static void OnSelectedDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonitorVisualMap map)
        {
            map.Redraw();
        }
    }

    private void HookItemPropertyChanged(IEnumerable<DisplayInfo>? items, bool hook)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (hook)
                item.PropertyChanged += OnItemPropertyChanged;
            else
                item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HookItemPropertyChanged(e.OldItems?.Cast<DisplayInfo>(), false);
        HookItemPropertyChanged(e.NewItems?.Cast<DisplayInfo>(), true);
        Redraw();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DisplayInfo.IsExcluded) or nameof(DisplayInfo.IsTargetPrimary) or nameof(DisplayInfo.FriendlyName))
        {
            Dispatcher.Invoke(Redraw);
        }
    }

    public void Redraw()
    {
        MapCanvas.Children.Clear();

        var displays = DisplaysSource?.ToList();
        if (displays == null || displays.Count == 0)
        {
            EmptyNotice.Visibility = Visibility.Visible;
            return;
        }

        EmptyNotice.Visibility = Visibility.Collapsed;

        double availableW = ActualWidth > 50 ? ActualWidth - 50 : 660;
        double availableH = ActualHeight > 40 ? ActualHeight - 40 : 180;

        int minX = displays.Min(d => d.Left);
        int minY = displays.Min(d => d.Top);
        int maxX = displays.Max(d => d.Right);
        int maxY = displays.Max(d => d.Bottom);

        int totalSpanX = Math.Max(1, maxX - minX);
        int totalSpanY = Math.Max(1, maxY - minY);

        double scaleX = availableW / totalSpanX;
        double scaleY = availableH / totalSpanY;
        double scale = Math.Min(scaleX, scaleY);

        double totalCanvasW = Math.Round(totalSpanX * scale);
        double totalCanvasH = Math.Round(totalSpanY * scale);

        MapCanvas.Width = totalCanvasW;
        MapCanvas.Height = totalCanvasH;

        foreach (var d in displays)
        {
            double x = Math.Round((d.Left - minX) * scale);
            double y = Math.Round((d.Top - minY) * scale);
            double w = Math.Round(Math.Max(90, d.Width * scale));
            double h = Math.Round(Math.Max(70, d.Height * scale));

            bool isSelected = SelectedDisplay != null && (SelectedDisplay == d || SelectedDisplay.DeviceName == d.DeviceName);

            var border = new Border
            {
                Width = w,
                Height = h,
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                ToolTip = $"{d.DisplayTitle}\nResolution: {d.ResolutionString}\nStatus: {d.StatusDescription}\nClick to select"
            };

            if (isSelected)
            {
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
                border.BorderThickness = new Thickness(2.5);
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 37, 99, 235));
                border.Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(59, 130, 246),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }
            else
            {
                border.BorderThickness = new Thickness(1.5);
                border.Effect = null;

                if (d.IsTargetPrimary)
                {
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 16, 185, 129));
                }
                else if (d.IsExcluded)
                {
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 239, 68, 68));
                }
                else
                {
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(82, 82, 91));
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 255, 255));
                }
            }

            var grid = new Grid
            {
                Margin = new Thickness(8, 6, 8, 6)
            };

            // Top-left compact badge for monitor index (Requirement 3: clean & not overwhelming)
            var indexBadge = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(6),
                Background = isSelected
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235))
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 30, 30, 35)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            indexBadge.Child = new TextBlock
            {
                Text = d.Index.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            grid.Children.Add(indexBadge);

            // Center Content: Display Title and Resolution
            var centerStack = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var nameText = new TextBlock
            {
                Text = d.DisplayTitle,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = Math.Max(50, w - 16)
            };
            centerStack.Children.Add(nameText);

            var resText = new TextBlock
            {
                Text = d.ResolutionString,
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            centerStack.Children.Add(resText);

            // Status label
            string statusStr = d.IsTargetPrimary ? "PRIMARY TARGET" : (d.IsExcluded ? "BLOCKED ⛔" : "ALLOWED");
            var statusColor = d.IsTargetPrimary
                ? System.Windows.Media.Color.FromRgb(52, 211, 153)
                : (d.IsExcluded ? System.Windows.Media.Color.FromRgb(248, 113, 113) : System.Windows.Media.Color.FromRgb(161, 161, 170));

            var statusText = new TextBlock
            {
                Text = statusStr,
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(statusColor),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            centerStack.Children.Add(statusText);

            grid.Children.Add(centerStack);
            border.Child = grid;

            border.MouseLeftButtonUp += (s, e) =>
            {
                SelectedDisplay = d;
                MonitorSelected?.Invoke(d);
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            MapCanvas.Children.Add(border);
        }
    }
}
