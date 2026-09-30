using System.Text.Json;
using System.Text.Json.Serialization;
using Nobitex.Net.Models;

namespace Nobitex.Net.Serialization;

/// <summary>
/// Central JSON configuration used by every REST call of the library.
/// Nobitex returns <c>camelCase</c> properties and encodes monetary values as strings,
/// therefore the deserializer is configured to be lenient about number/string differences.
/// </summary>
public static class NobitexJson
{
    /// <summary>Shared serializer options (read &amp; write).</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        options.Converters.Add(new DecimalStringConverter());
        options.Converters.Add(new UnixMillisecondsDateTimeOffsetConverter());
        options.Converters.Add(new OrderBookLevelConverter());

        return options;
    }
}

/// <summary>
/// Converts a single order book level.
/// </summary>
/// <remarks>
/// <para>
/// Nobitex serialises every price level as a two element <b>array</b> of strings, for example:
/// </para>
/// <code>
/// "bids": [ ["1470001120", "0.126571"], ["1470000000", "0.818994"] ]
/// </code>
/// <para>
/// Without this converter <c>System.Text.Json</c> throws
/// <c>The JSON value could not be converted to Nobitex.Net.Models.OrderBookLevel</c>, because it
/// tries to map a JSON array onto the named members <c>Price</c> and <c>Amount</c>.
/// The converter also accepts the object form (<c>{"price": "...", "amount": "..."}</c>) which is
/// used by some WebSocket payloads, so both shapes work transparently.
/// </para>
/// </remarks>
public sealed class OrderBookLevelConverter : JsonConverter<OrderBookLevel>
{
    /// <inheritdoc />
    public override OrderBookLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // --- array form: ["price", "amount"] (Nobitex REST /v3/orderbook) -------------
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            var price = ReadDecimal(ref reader);

            reader.Read();
            var amount = ReadDecimal(ref reader);

            // Skip any extra elements the API might add in the future (forward compatibility).
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                reader.Skip();
            }

            return new OrderBookLevel(price, amount);
        }

        // --- object form: { "price": "...", "amount": "..." } -------------------------
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            decimal objectPrice = 0m, objectAmount = 0m;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propertyName = reader.GetString();
                reader.Read();

                if (string.Equals(propertyName, "price", StringComparison.OrdinalIgnoreCase))
                {
                    objectPrice = ReadDecimal(ref reader);
                }
                else if (string.Equals(propertyName, "amount", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(propertyName, "quantity", StringComparison.OrdinalIgnoreCase))
                {
                    objectAmount = ReadDecimal(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }

            return new OrderBookLevel(objectPrice, objectAmount);
        }

        throw new JsonException($"Cannot convert token {reader.TokenType} to an order book level.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OrderBookLevel value, JsonSerializerOptions options)
    {
        // Write back in the same array shape Nobitex uses.
        writer.WriteStartArray();
        writer.WriteStringValue(value.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteStringValue(value.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteEndArray();
    }

    /// <summary>Reads a decimal that may be encoded either as a JSON string or as a number.</summary>
    private static decimal ReadDecimal(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.String => decimal.Parse(
            reader.GetString()!,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture),
        JsonTokenType.Number => reader.GetDecimal(),
        JsonTokenType.Null => 0m,
        _ => throw new JsonException($"Cannot convert token {reader.TokenType} to decimal."),
    };
}

/// <summary>
/// Converts monetary values. Nobitex sends amounts/prices as strings with up to 10 decimals;
/// .NET <see cref="decimal"/> is used to keep full precision and the value is written back
/// as a string (as recommended by the Nobitex documentation).
/// </summary>
public sealed class DecimalStringConverter : JsonConverter<decimal>
{
    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => decimal.Parse(
                reader.GetString()!,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetDecimal(),
            JsonTokenType.Null => 0m,
            _ => throw new JsonException($"Cannot convert token {reader.TokenType} to decimal."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>
/// Converts Nobitex unix timestamps expressed in milliseconds
/// (for example <c>"lastUpdate": 1726651067347</c>) into <see cref="DateTimeOffset"/>.
/// </summary>
public sealed class UnixMillisecondsDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        long milliseconds = reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => long.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new JsonException($"Cannot convert token {reader.TokenType} to DateTimeOffset."),
        };

        return Epoch.AddMilliseconds(milliseconds);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteNumberValue((long)value.ToUnixTimeMilliseconds());
}

/// <summary>
/// Helper used to build request payloads. Collections and <c>null</c> members are skipped so the
/// outgoing JSON matches exactly what the Nobitex API expects.
/// </summary>
public static class NobitexPayload
{
    /// <summary>Serialises an anonymous object into the raw JSON body string.</summary>
    public static string ToJson(object payload) => JsonSerializer.Serialize(payload, NobitexJson.Options);

    /// <summary>Deserialises a raw JSON string into a strongly typed model.</summary>
    public static T? FromJson<T>(string json) where T : class
        => JsonSerializer.Deserialize<T>(json, NobitexJson.Options);
}