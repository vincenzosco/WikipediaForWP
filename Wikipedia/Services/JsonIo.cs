using System;
using System.Globalization;
using Windows.Data.Json;

namespace Wikipedia.Services
{
    /// <summary>
    /// Small helpers over Windows.Data.Json. The action and REST responses
    /// hand back explicit JSON nulls for optional members, and every typed
    /// GetNamed* overload throws on those, so reads always go through the
    /// value type check here.
    /// </summary>
    internal static class JsonIo
    {
        internal static JsonObject Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            try
            {
                return JsonObject.Parse(text);
            }
            catch (Exception)
            {
                // A corrupt cache entry should be treated as a cache miss.
                return null;
            }
        }

        internal static string ReadString(JsonObject source, string name)
        {
            if (source == null || !source.ContainsKey(name))
            {
                return null;
            }

            var value = source.GetNamedValue(name);

            if (value == null || value.ValueType != JsonValueType.String)
            {
                return null;
            }

            return value.GetString();
        }

        internal static int ReadNumber(JsonObject source, string name, int fallback)
        {
            if (source == null || !source.ContainsKey(name))
            {
                return fallback;
            }

            var value = source.GetNamedValue(name);

            if (value == null || value.ValueType != JsonValueType.Number)
            {
                return fallback;
            }

            return (int)value.GetNumber();
        }

        /// <summary>
        /// Reads an object member, or <c>null</c> when it is missing or is not
        /// an object. The two argument GetNamedObject overload only falls back
        /// for a missing key; a key that is present with another type still
        /// throws, and the search endpoint sends "thumbnail": null for every
        /// result without an image.
        /// </summary>
        internal static JsonObject ReadObject(JsonObject source, string name)
        {
            if (source == null || !source.ContainsKey(name))
            {
                return null;
            }

            var value = source.GetNamedValue(name);

            if (value == null || value.ValueType != JsonValueType.Object)
            {
                return null;
            }

            return value.GetObject();
        }

        /// <summary>
        /// Reads an array member with the same guard as <see cref="ReadObject"/>:
        /// a key that is present but holds another type reads as absent instead
        /// of throwing.
        /// </summary>
        internal static JsonArray ReadArray(JsonObject source, string name)
        {
            if (source == null || !source.ContainsKey(name))
            {
                return null;
            }

            var value = source.GetNamedValue(name);

            if (value == null || value.ValueType != JsonValueType.Array)
            {
                return null;
            }

            return value.GetArray();
        }

        internal static bool ReadBoolean(JsonObject source, string name, bool fallback)
        {
            if (source == null || !source.ContainsKey(name))
            {
                return fallback;
            }

            var value = source.GetNamedValue(name);

            if (value == null || value.ValueType != JsonValueType.Boolean)
            {
                return fallback;
            }

            return value.GetBoolean();
        }

        internal static void SetString(JsonObject target, string name, string value)
        {
            // Nulls are never written, they would come back as JSON null and
            // have no meaning for any of the stored types.
            target.SetNamedValue(name, JsonValue.CreateStringValue(value ?? string.Empty));
        }

        internal static void SetNumber(JsonObject target, string name, int value)
        {
            target.SetNamedValue(name, JsonValue.CreateNumberValue(value));
        }

        internal static void SetBoolean(JsonObject target, string name, bool value)
        {
            target.SetNamedValue(name, JsonValue.CreateBooleanValue(value));
        }

        internal static string FormatDate(DateTimeOffset value)
        {
            // Round trip format keeps the offset, so a stored timestamp reads
            // back as the same instant regardless of the device time zone.
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        internal static DateTimeOffset ReadDate(JsonObject source, string name, DateTimeOffset fallback)
        {
            var text = ReadString(source, name);

            if (string.IsNullOrEmpty(text))
            {
                return fallback;
            }

            DateTimeOffset parsed;

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
