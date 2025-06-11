using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmiiboGameList.Models;

[JsonConverter(typeof(HexJsonConverter))]
[TypeConverter(typeof(HexTypeConverter))]
public sealed class Hex(ulong value) : IComparable<Hex>, IEquatable<Hex>
{
    public ulong Value { get; } = value;

    public static implicit operator Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        return new Hex(Convert.ToUInt64(value, 16));
    }

    public override string ToString() => "0x" + Value.ToString("x16");

    public int CompareTo(Hex other)
    {
        return other is null ? 1 : Value.CompareTo(other.Value);
    }

    public bool Equals(Hex other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object obj) => obj is Hex other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(Hex left, Hex right) => Equals(left, right);
    public static bool operator !=(Hex left, Hex right) => !Equals(left, right);
}

internal class HexTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        return value is string stringValue ? (Hex)stringValue : base.ConvertFrom(context, culture, value);
    }

    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
    {
        return destinationType == typeof(string) && value is Hex hexValue
            ? hexValue.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
    }
}

internal class HexJsonConverter : JsonConverter<Hex>
{
    public override Hex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string s = reader.GetString();
            return (Hex)s;
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, Hex value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.ToString());
    }
}