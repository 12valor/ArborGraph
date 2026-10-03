using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskScope.Infrastructure;

public class BytesToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long longVal) return SizeFormatter.Format(longVal);
        if (value is int intVal) return SizeFormatter.Format(intVal);
        if (value is double dblVal) return SizeFormatter.Format((long)dblVal);
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NumberFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long longVal) return SizeFormatter.FormatCount(longVal);
        if (value is int intVal) return SizeFormatter.FormatCount(intVal);
        if (value is double dblVal) return dblVal.ToString("N1", culture);
        return "0";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            if (dt == DateTime.MinValue) return "—";
            return dt.ToString("yyyy-MM-dd HH:mm:ss", culture);
        }
        return "—";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = false;
        if (value is bool boolVal) b = boolVal;
        else if (value is int intVal) b = intVal > 0;
        else if (value is long longVal) b = longVal > 0;
        return b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class TabActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is string currentTab && values[1] is string tabParam)
        {
            return string.Equals(currentTab, tabParam, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PercentageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"{d:F1}%";
        }
        return "0.0%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
