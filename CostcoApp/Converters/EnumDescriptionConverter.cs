using System;
using System.Globalization;
using System.Windows.Data;
using CostcoDeals.Shared.Utilities;

namespace CostcoApp.Converters
{
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Enum e ? EnumHelper.GetDescription(e) : value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}