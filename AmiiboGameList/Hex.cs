using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;

namespace AmiiboGameList
{
    [JsonConverter(typeof(ToStringJsonConverter))]
    [TypeConverter(typeof(HexClassConverter))]
    public partial class Hex
    {
        public ulong HexValue;

        public Hex(ulong HexValue)
        {
            this.HexValue = HexValue;
        }

        // Converters
        public static explicit operator Hex(string value)
        {
            return new Hex(Convert.ToUInt64(value, 16));
        }
        public override string ToString()
        {
            return "0x" + HexValue.ToString("x16");
        }
    }

    // Comparer for sorting
    public partial class Hex : IComparable
    {
        public int CompareTo(object obj)
        {
            return HexValue.CompareTo(((Hex)obj).HexValue);
        }
    }

    public class HexClassConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context,
        Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }
        public override object ConvertFrom(ITypeDescriptorContext context,
         CultureInfo culture, object value)
        {
            if (value is string @string)
            {
                return new Hex(Convert.ToUInt64(@string, 16));
            }
            return base.ConvertFrom(context, culture, value);
        }
        public override object ConvertTo(ITypeDescriptorContext context,
        CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string)) { return ((Hex)value).ToString(); }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class ToStringJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
