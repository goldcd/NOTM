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

        double availableW = ActualWidth > 60 ? ActualWidth - 60 : 640;
        double availableH = ActualHeight > 50 ? ActualHeight - 50 : 160;

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
            double w = Math.Max(60, d.Width * scale);
            double h = Math.Max(48, d.Height * scale);

            bool isSelected = SelectedDisplay != null && (SelectedDisplay == d || SelectedDisplay.DeviceName == d.DeviceName);

            var border = new Border
            {
                Width = w,
                Height = h,
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                ToolTip = $"{d.DisplayTitle}\nResolution: {d.ResolutionString}\nStatus: {d.StatusDescription}\nClick to select this display"
            };

            // Replicate Windows 11 Display Settings monitor block style
            if (isSelected)
            {
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)); // Selected Accent Blue
                border.BorderThickness = new Thickness(3);
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 37, 99, 235));
                border.Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(59, 130, 246),
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.4
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
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 255, 255, 255));
                }
            }

            var stack = new StackPanel
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(4)
            };

            // Monitor Number [1] or [2] (Large and bold like Windows Display settings)
            var indexText = new TextBlock
            {
                Text = d.Index.ToString(),
                FontSize = Math.Max(18, Math.Min(34, h * 0.40)),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            stack.Children.Add(indexText);

            // Display Title / Friendly Name
            var nameText = new TextBlock
            {
                Text = d.DisplayTitle,
                FontSize = Math.Max(10, Math.Min(12, h * 0.16)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(228, 228, 231)),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = Math.Max(40, w - 10)
            };
            stack.Children.Add(nameText);

            // Status Badge
            string statusStr;
            SolidColorBrush badgeColor;
            if (d.IsTargetPrimary)
            {
                statusStr = "PRIMARY TARGET";
                badgeColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
            }
            else if (d.IsExcluded)
            {
                statusStr = "BLOCKED ⛔";
                badgeColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113));
            }
            else
            {
                statusStr = "ALLOWED";
                badgeColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170));
            }

            var statusText = new TextBlock
            {
                Text = statusStr,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = badgeColor,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(statusText);

            border.Child = stack;

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
