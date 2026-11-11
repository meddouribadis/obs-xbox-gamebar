using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace ObsGameBarWidget.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class SceneBackgroundConverter : IValueConverter
    {
        private static SolidColorBrush ActiveBrush => new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
        private static SolidColorBrush InactiveBrush => new SolidColorBrush(Color.FromArgb(255, 42, 45, 54));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? ActiveBrush : InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class SceneFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? FontWeights.Bold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MuteBackgroundConverter : IValueConverter
    {
        private static SolidColorBrush MutedBrush => new SolidColorBrush(Color.FromArgb(255, 209, 52, 56)); // Red
        private static SolidColorBrush UnmutedBrush => new SolidColorBrush(Color.FromArgb(255, 54, 58, 71)); // Dark grey

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? MutedBrush : UnmutedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MuteIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "\uE74F" : "\uE720"; // Mic Off vs Mic On
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MuteTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "Mute" : "Actif";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}

