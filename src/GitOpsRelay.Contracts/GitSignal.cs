using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitOpsRelay.Contracts;

public sealed class GitSignal
{
    private IDictionary<string, JsonElement>? _extensions;

    public string Specversion { get; init; } = "1.0";
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Type { get; init; }
    public required string Subject { get; init; }
    public required DateTimeOffset Time { get; init; }
    public string Datacontenttype { get; init; } = "application/json";
    public string? Dataschema { get; init; }
    public required string Idempotencykey { get; init; }
    public required GitSignalData Data { get; init; }
    public string? Targetref { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extensions
    {
        get => _extensions;
        set => _extensions = value is null ? null : new ValidatedExtensionDictionary(value);
    }

    public static JsonElement CreateExtensionValue(object value) => value switch
    {
        string text => JsonSerializer.SerializeToElement(text),
        bool boolean => JsonSerializer.SerializeToElement(boolean),
        int integer => JsonSerializer.SerializeToElement(integer),
        long integer => JsonSerializer.SerializeToElement(integer),
        Uri uri => JsonSerializer.SerializeToElement(uri.AbsoluteUri),
        _ => throw new ArgumentException("CloudEvents extension values must be string, bool, integer, or Uri scalars.", nameof(value))
    };

    private sealed class ValidatedExtensionDictionary : IDictionary<string, JsonElement>
    {
        private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
        {
            "specversion", "id", "source", "type", "subject", "time", "datacontenttype",
            "dataschema", "data", "idempotencykey", "targetref"
        };

        private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);

        public ValidatedExtensionDictionary(IEnumerable<KeyValuePair<string, JsonElement>> initial)
        {
            foreach (var (key, value) in initial)
            {
                Add(key, value);
            }
        }

        public JsonElement this[string key]
        {
            get => _values[key];
            set
            {
                Validate(key, value);
                _values[key] = value.Clone();
            }
        }

        public ICollection<string> Keys => _values.Keys;
        public ICollection<JsonElement> Values => _values.Values;
        public int Count => _values.Count;
        public bool IsReadOnly => false;
        public void Add(string key, JsonElement value)
        {
            Validate(key, value);
            _values.Add(key, value.Clone());
        }
        public void Add(KeyValuePair<string, JsonElement> item) => Add(item.Key, item.Value);
        public void Clear() => _values.Clear();
        public bool Contains(KeyValuePair<string, JsonElement> item) =>
            ((ICollection<KeyValuePair<string, JsonElement>>)_values).Contains(item);
        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, JsonElement>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, JsonElement>>)_values).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator() => _values.GetEnumerator();
        public bool Remove(string key) => _values.Remove(key);
        public bool Remove(KeyValuePair<string, JsonElement> item) =>
            ((ICollection<KeyValuePair<string, JsonElement>>)_values).Remove(item);
        public bool TryGetValue(string key, out JsonElement value) => _values.TryGetValue(key, out value);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private static void Validate(string key, JsonElement value)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 20 || key[0] is < 'a' or > 'z'
                || key.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
                || ReservedNames.Contains(key))
            {
                throw new ArgumentException($"'{key}' is not an allowed CloudEvents extension name.", nameof(key));
            }

            var validNumber = value.ValueKind != JsonValueKind.Number || value.TryGetInt64(out _);
            if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.True or JsonValueKind.False
                or JsonValueKind.Number) || !validNumber)
            {
                throw new ArgumentException("CloudEvents extension values must be primitive scalars.", nameof(value));
            }
        }
    }
}
