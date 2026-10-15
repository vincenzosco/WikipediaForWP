using System;
using System.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Wikipedia.Services;

namespace Wikipedia.Converters
{
    /// <summary>
    /// Collapses an element when its bound string is null or whitespace.
    /// Useful for optional fields such as the search excerpt or thumbnail.
    /// </summary>
    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var text = value as string;
            var visible = !string.IsNullOrWhiteSpace(text);

            // Pass "invert" to show the element when the string is empty.
            var invert = parameter as string;
            if (string.Equals(invert, "invert", StringComparison.OrdinalIgnoreCase))
            {
                visible = !visible;
            }

            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Renders a timestamp as a localized relative phrase such as
    /// "5 minuti fa". The templates live in the .resw files so the wording
    /// follows the device language.
    /// </summary>
    public sealed class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var localizer = Localizer.Current;

            if (localizer == null)
            {
                return string.Empty;
            }

            if (!(value is DateTimeOffset))
            {
                return string.Empty;
            }

            var moment = (DateTimeOffset)value;
            var elapsed = DateTimeOffset.Now - moment;

            // Clock skew or a future timestamp should not read as negative.
            if (elapsed.TotalSeconds < 60)
            {
                return localizer.Get("Time_JustNow");
            }

            if (elapsed.TotalMinutes < 60)
            {
                return localizer.Format("Time_MinutesAgo", (int)elapsed.TotalMinutes);
            }

            if (elapsed.TotalHours < 24)
            {
                return localizer.Format("Time_HoursAgo", (int)elapsed.TotalHours);
            }

            if (elapsed.TotalDays < 30)
            {
                return localizer.Format("Time_DaysAgo", (int)elapsed.TotalDays);
            }

            return moment.ToString("d", CultureInfo.CurrentCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Maps a bool to Visibility. Pass "invert" as the converter parameter to
    /// show the element when the value is false, which is how the empty state
    /// of a list is driven by its own item count.
    /// </summary>
    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var flag = value is bool && (bool)value;

            var invert = parameter as string;
            if (string.Equals(invert, "invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Maps a bool to FlowDirection. The language picker holds editions in both
    /// directions at once, so each row is laid out the way its own edition is
    /// written rather than the way the app around it is.
    /// </summary>
    public sealed class BooleanToFlowDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var flag = value is bool && (bool)value;

            return flag ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
