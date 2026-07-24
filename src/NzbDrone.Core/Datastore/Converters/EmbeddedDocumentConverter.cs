using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Datastore.Converters
{
    public class EmbeddedDocumentConverter<T> : SqlMapper.TypeHandler<T>
    {
        protected readonly JsonSerializerOptions SerializerSettings;

        // Matches bare (unquoted) property names in JS-object-literal-style JSON:
        //   {quality: 1, revision: {version: 1}}  →  {"quality": 1, "revision": {"version": 1}}
        private static readonly Regex BareKeyPattern = new Regex(
            @"(?<=[\{,])\s*(\w+)\s*(?=:)", RegexOptions.Compiled);

        public EmbeddedDocumentConverter()
        {
            var serializerSettings = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            serializerSettings.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, true));
            serializerSettings.Converters.Add(new STJTimeSpanConverter());
            serializerSettings.Converters.Add(new STJUtcConverter());

            SerializerSettings = serializerSettings;
        }

        public EmbeddedDocumentConverter(params JsonConverter[] converters)
            : this()
        {
            foreach (var converter in converters)
            {
                SerializerSettings.Converters.Add(converter);
            }
        }

        public override void SetValue(IDbDataParameter parameter, T value)
        {
            // Cast to object to get all properties written out
            // https://github.com/dotnet/corefx/issues/38650
            parameter.Value = JsonSerializer.Serialize((object)value, SerializerSettings);
        }

        public override T Parse(object value)
        {
            var json = (string)value;

            try
            {
                return JsonSerializer.Deserialize<T>(json, SerializerSettings);
            }
            catch (JsonException)
            {
                // Legacy Readarr databases may store embedded documents with
                // unquoted property names (JS object-literal style). Sanitize
                // and retry so the row can be read; SetValue will re-serialize
                // in proper JSON on next write.
                var sanitized = BareKeyPattern.Replace(json, " \"$1\"");

                if (sanitized == json)
                {
                    throw;
                }

                return JsonSerializer.Deserialize<T>(sanitized, SerializerSettings);
            }
        }
    }
}
