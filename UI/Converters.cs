using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NOTM.UI;

public class BlockButtonTextConverter : IValueConverter
{
    public static readonly BlockButtonTextConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExcluded && isExcluded)
        {
            return "Allow Display";
        }
        return "Block Display";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BlockButtonStyleConverter : IValueConverter
{
    public static readonly BlockButtonStyleConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Application.Current?.MainWindow is Window window)
        {
            if (value is bool isExcluded && isExcluded)
            {
                return window.FindResource("SuccessButtonStyle");
            }
            return window.FindResource("DangerButtonStyle");
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ZeroCountToVisibilityConverter : IValueConverter
{
    public static readonly ZeroCountToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count && count > 0)
        {
            return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
