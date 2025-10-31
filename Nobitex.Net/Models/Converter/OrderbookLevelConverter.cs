using Nobitex.Net;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

// converter for OrderbookLevel represented as [ price, amount ]
public class OrderbookLevelConverter : JsonConverter<OrderbookLevel>
{
    public override OrderbookLevel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for OrderbookLevel");

        // move to first element
        if (!reader.Read())
            throw new JsonException("Unexpected end when reading OrderbookLevel");

        string? price = null;
        string? amount = null;

        // first element -> price
        if (reader.TokenType == JsonTokenType.String)
            price = reader.GetString();
        else if (reader.TokenType == JsonTokenType.Number)
            price = reader.GetString();
        else if (reader.TokenType == JsonTokenType.Null)
            price = null;
        else
            throw new JsonException($"Unexpected token for price: {reader.TokenType}");

        // move to second element
        if (!reader.Read())
            throw new JsonException("Unexpected end when reading OrderbookLevel element 2");

        if (reader.TokenType == JsonTokenType.String)
            amount = reader.GetString();
        else if (reader.TokenType == JsonTokenType.Number)
            amount = reader.GetString();
        else if (reader.TokenType == JsonTokenType.Null)
            amount = null;
        else
            throw new JsonException($"Unexpected token for amount: {reader.TokenType}");

        // advance to end array
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Expected end of array after OrderbookLevel elements");

        return new OrderbookLevel(Price: price, Amount: amount);
    }

    public override void Write(Utf8JsonWriter writer, OrderbookLevel value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value.Price is null)
            writer.WriteNullValue();
        else if (decimal.TryParse(value.Price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            writer.WriteStringValue(value.Price);
        else
            writer.WriteStringValue(value.Price);
        if (value.Amount is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Amount);
        writer.WriteEndArray();
    }
}
