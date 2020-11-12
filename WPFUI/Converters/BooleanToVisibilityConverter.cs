using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Project.Converters
{
    /// <summary>
    /// Contains the converter and convertback methods for the boolean to visibility conversions
    /// </summary>
    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Used to convert a boolean to a visibility
        /// </summary>
        /// <param name="value">This is the boolean input</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>Returns a visibility</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
            {
                //If there is an issue with the input, return collapsed
                return Visibility.Collapsed;
            }
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Used to take a visibility and returns a visibility
        /// </summary>
        /// <param name="value">This is the boolean input</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>Returns a visibility</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Visibility))
            {
                //If there is an issue wtih the input, return collapsed
                return Visibility.Collapsed;
            }
            return (Visibility)value == Visibility.Visible;
        }
    }
}