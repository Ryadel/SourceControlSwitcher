using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace SourceControlSwitcher.Options
{
    /// <summary>
    /// Shows enum members by their <see cref="DescriptionAttribute"/> in the Tools > Options property grid
    /// (the default <see cref="EnumConverter"/> shows the raw member names).
    /// </summary>
    public sealed class EnumDescriptionConverter : EnumConverter
    {
        public EnumDescriptionConverter(Type type)
            : base(type)
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null && value.GetType() == EnumType)
            {
                var field = EnumType.GetField(Enum.GetName(EnumType, value) ?? string.Empty);
                var description = field?.GetCustomAttribute<DescriptionAttribute>();
                if (description != null)
                    return description.Description;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                foreach (var field in EnumType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var description = field.GetCustomAttribute<DescriptionAttribute>();
                    if (description != null && string.Equals(description.Description, text, StringComparison.OrdinalIgnoreCase))
                        return field.GetValue(null);
                }
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
