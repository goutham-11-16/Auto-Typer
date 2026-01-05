using System;
using System.Windows.Markup;

namespace AutoTyper
{
    public class EnumBindingSource : MarkupExtension
    {
        public Type EnumType { get; set; }

        public EnumBindingSource() { }

        public EnumBindingSource(Type enumType)
        {
            this.EnumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (null == this.EnumType)
                throw new InvalidOperationException("The EnumType must be specified.");

            var actualEnumType = Nullable.GetUnderlyingType(this.EnumType) ?? this.EnumType;
            var enumValues = Enum.GetValues(actualEnumType);

            if (actualEnumType == this.EnumType)
                return enumValues;

            var tempArray = Array.CreateInstance(actualEnumType, enumValues.Length + 1);
            enumValues.CopyTo(tempArray, 1);
            return tempArray;
        }
    }
}
