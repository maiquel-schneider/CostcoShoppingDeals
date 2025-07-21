using System;
using System.ComponentModel;
using System.Reflection;

namespace CostcoDeals.Shared.Utilities
{
    /// <summary>
    /// Helper methods for working with enums:
    /// - <see cref="GetDescription"/> to read the [Description] attribute.
    /// </summary>
    public static class EnumHelper
    {
        /// <summary>
        /// Returns the text from a <see cref="DescriptionAttribute"/> on the enum value,
        /// or falls back to <c>value.ToString()</c> if none is present.
        /// </summary>
        public static string GetDescription(Enum value)
        {
            return value
                .GetType()
                .GetField(value.ToString())
                ?.GetCustomAttribute<DescriptionAttribute>()
                ?.Description
                ?? value.ToString();
        }
    }
}
