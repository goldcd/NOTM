using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

    public IEnumerable<DisplayInfo>? DisplaysSource
    {
        get => (IEnumerable<DisplayInfo>?)GetValue(DisplaysSourceProperty);
        set => SetValue(DisplaysSourceProperty, value);
    }

    public event Action<DisplayInfo>? MonitorClicked;

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
        if (e.PropertyName is nameof(DisplayInfo.IsExcluded) or nameof(DisplayInfo.IsTargetPrimary))
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

        double availableW = ActualWidth > 40 ? ActualWidth - 40 : 540;
        double availableH = ActualHeight > 40 ? ActualHeight - 40 : 120;

        int minX = displays.Min(d => d.Left);
        int minY = displays.Min(d => d.Top);
        int maxX = displays.Max(d => d.Right);
        int maxY = displays.Max(d => d.Bottom);

        int totalSpanX = Math.Max(1, maxX - minX);
        int totalSpanY = Math.Max(1, maxY - minY);

        double scaleX = availableW / totalSpanX;
        double scaleY = availableH / totalSpanY;
        double scale = Math.Min(scaleX, scaleY);

        double totalCanvasW = totalSpanX * scale;
        double totalCanvasH = totalSpanY * scale;

        MapCanvas.Width = totalCanvasW;
        MapCanvas.Height = totalCanvasH;

        foreach (var d in displays)
        {
            double x = (d.Left - minX) * scale;
            double y = (d.Top - minY) * scale;
            double w = Math.Max(40, d.Width * scale);
            double h = Math.Max(30, d.Height * scale);

            var border = new Border
            {
                Width = w,
                Height = h,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                ToolTip = $"{d.DisplayTitle}\n{d.ResolutionString}\nStatus: {d.StatusDescription}"
            };

            if (d.IsTargetPrimary)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 16, 185, 129));
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            }
            else if (d.IsExcluded)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 239, 68, 68));
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            }
            else
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(113, 113, 122));
            }

            var stack = new StackPanel
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(4)
            };

            // Badge / Index
            var indexText = new TextBlock
            {
                Text = d.Index.ToString(),
                FontSize = Math.Max(13, Math.Min(18, h * 0.28)),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            stack.Children.Add(indexText);

            // Friendly Name (truncated if needed)
            var nameText = new TextBlock
            {
                Text = d.DisplayTitle,
                FontSize = Math.Max(10, Math.Min(12, h * 0.18)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 212, 216)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = Math.Max(30, w - 8)
            };
            stack.Children.Add(nameText);

            // Status label
            var statusText = new TextBlock
            {
                Text = d.IsTargetPrimary ? "TARGET" : (d.IsExcluded ? "BLOCKED" : "ALLOWED"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = d.IsTargetPrimary
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153))
                    : (d.IsExcluded ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170))),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            stack.Children.Add(statusText);

            border.Child = stack;

            border.MouseLeftButtonUp += (s, e) =>
            {
                MonitorClicked?.Invoke(d);
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            MapCanvas.Children.Add(border);
        }
    }
}
