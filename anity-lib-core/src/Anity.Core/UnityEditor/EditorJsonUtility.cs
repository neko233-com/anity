using System;
using System.Reflection;
using System.Text.Json;

namespace UnityEditor
{
    public static class EditorJsonUtility
    {
        private static readonly JsonSerializerOptions _defaultOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions _prettyOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            WriteIndented = true
        };

        public static string ToJson(object? obj, bool prettyPrint = false)
        {
            if (obj is null) return "{}";
            var options = prettyPrint ? _prettyOptions : _defaultOptions;
            var allFieldsOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IncludeFields = true,
                WriteIndented = prettyPrint
            };
            return JsonSerializer.Serialize(obj, obj.GetType(), allFieldsOptions);
        }

        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
            if (string.IsNullOrWhiteSpace(json) || objectToOverwrite is null) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                var type = objectToOverwrite.GetType();
                foreach (JsonProperty prop in root.EnumerateObject())
                {
                    string name = char.ToUpperInvariant(prop.Name[0]) + prop.Name[1..];

                    FieldInfo? field = null;
                    var t = type;
                    while (t != null && t != typeof(object))
                    {
                        field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (field != null) break;
                        t = t.BaseType;
                    }

                    if (field is not null)
                    {
                        object? value = DeserializeValue(prop.Value, field.FieldType);
                        field.SetValue(objectToOverwrite, value);
                        continue;
                    }

                    PropertyInfo? property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (property is not null && property.CanWrite)
                    {
                        object? value = DeserializeValue(prop.Value, property.PropertyType);
                        property.SetValue(objectToOverwrite, value);
                    }
                }
            }
            catch { }
        }

        private static object? DeserializeValue(JsonElement element, Type targetType)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                    return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
                case JsonValueKind.Number:
                    if (targetType == typeof(int)) return element.GetInt32();
                    if (targetType == typeof(long)) return element.GetInt64();
                    if (targetType == typeof(float)) return element.GetSingle();
                    if (targetType == typeof(double)) return element.GetDouble();
                    if (targetType == typeof(bool)) return element.GetBoolean();
                    return Convert.ChangeType(element.GetDouble(), targetType);
                case JsonValueKind.String:
                    string str = element.GetString() ?? string.Empty;
                    if (targetType == typeof(string)) return str;
                    if (targetType.IsEnum) return Enum.Parse(targetType, str);
                    return str;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Object:
                    return JsonSerializer.Deserialize(element.GetRawText(), targetType, _defaultOptions);
                case JsonValueKind.Array:
                    return JsonSerializer.Deserialize(element.GetRawText(), targetType, _defaultOptions);
                default:
                    return default;
            }
        }
    }
}
