namespace TextChunker.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A JSON converter factory that serializes enums as their string names and rejects unknown string values on
    /// read, rather than silently binding an unrecognized value to the zero member.
    /// </summary>
    public class StrictEnumConverterFactory : JsonConverterFactory
    {
        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert == null) throw new ArgumentNullException(nameof(typeToConvert));
            return typeToConvert.IsEnum;
        }

        /// <inheritdoc />
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert == null) throw new ArgumentNullException(nameof(typeToConvert));

            Type converterType = typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert);
            object? converter = Activator.CreateInstance(converterType);
            if (converter == null)
                throw new InvalidOperationException("Unable to create a strict enum converter for " + typeToConvert.FullName + ".");

            return (JsonConverter)converter;
        }

        private sealed class StrictEnumConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException("Expected a string value for enum " + typeToConvert.Name + ".");

                string? value = reader.GetString();
                if (string.IsNullOrEmpty(value))
                    throw new JsonException("Empty string is not a valid value for enum " + typeToConvert.Name + ".");

                if (Enum.TryParse<T>(value, ignoreCase: true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
                    return parsed;

                throw new JsonException("'" + value + "' is not a valid value for enum " + typeToConvert.Name + ".");
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                if (writer == null) throw new ArgumentNullException(nameof(writer));
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
