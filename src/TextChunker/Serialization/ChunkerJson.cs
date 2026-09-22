namespace TextChunker.Serialization
{
    using System.Text.Json;

    /// <summary>
    /// Centralizes the JSON serialization contract used for chunks, options, and fixtures so the on disk shape is
    /// defined in one place. Enums serialize as strings and unknown enum values are rejected on read.
    /// </summary>
    public static class ChunkerJson
    {
        /// <summary>
        /// Indented serializer options, suitable for fixtures and human readable export.
        /// </summary>
        public static readonly JsonSerializerOptions Indented = Build(true);

        /// <summary>
        /// Compact serializer options, suitable for storage and transport.
        /// </summary>
        public static readonly JsonSerializerOptions Compact = Build(false);

        /// <summary>
        /// Serialize a value to JSON using the indented contract.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="value">Value to serialize.</param>
        /// <returns>Indented JSON.</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Indented);
        }

        /// <summary>
        /// Deserialize JSON to a value using the shared contract.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON text.</param>
        /// <returns>The deserialized value, or null.</returns>
        public static T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Compact);
        }

        private static JsonSerializerOptions Build(bool indented)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new StrictEnumConverterFactory());
            return options;
        }
    }
}
