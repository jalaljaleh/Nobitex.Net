using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nobitex.Net;

public class OrderBookEntryConverter : JsonConverter<OrderBookEntry>
{
    public override OrderBookEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected array for OrderBookEntry.");

        reader.Read();
        var priceStr = reader.GetString();
        reader.Read();
        var volumeStr = reader.GetString();
        reader.Read(); // EndArray

        if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var price))
            price = 0;
        if (!decimal.TryParse(volumeStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var volume))
            volume = 0;

        return new OrderBookEntry(price, volume);
    }

    public override void Write(Utf8JsonWriter writer, OrderBookEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteStringValue(value.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteEndArray();
    }
}
