using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AFrame.Data
{
    public static class DefinitionSourceReader
    {
        public static string ComputeHash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")))
                .Replace("-", "").ToLowerInvariant();
        }

        public static bool TryRead(string path, out DefinitionDocument document, out string json, out string error)
        {
            document = null;
            json = "";
            error = "";
            try
            {
                json = File.ReadAllText(path);
                document = ReadHeader(ReadObject(json));
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is JsonException || exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        public const int CurrentVersion = 1;

        public static JObject ReadObject(string json) => JObject.Parse(json, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
        });

        public static DefinitionDocument ReadHeader(JObject data)
        {
            var id = data["id"];
            var version = data["schemaVersion"];
            var type = data["type"];
            if (id?.Type != JTokenType.String || !Regex.IsMatch((string)id, @"\A[a-z0-9]+([._-][a-z0-9]+)*\z"))
                throw new InvalidDataException("A definition requires a stable lowercase id.");
            if (version?.Type != JTokenType.Integer || !int.TryParse(version.ToString(), out var schemaVersion)
                || schemaVersion < 0 || schemaVersion > CurrentVersion)
                throw new InvalidDataException($"Unsupported definition schemaVersion. Expected 0 through {CurrentVersion}.");
            if (type?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)type))
                throw new InvalidDataException("A definition requires a type.");
            return new DefinitionDocument { id = (string)id, schemaVersion = schemaVersion, type = (string)type };
        }

        public static T ReadDefinition<T>(string json) where T : class, IDefinition =>
            (T)ReadDefinition(json, typeof(T));

        public static IDefinition ReadDefinition(string json, Type model)
        {
            var data = ReadObject(json);
            var header = ReadHeader(data);
            var type = (DefinitionTypeAttribute)Attribute.GetCustomAttribute(model, typeof(DefinitionTypeAttribute));
            if (type == null || type.Id != header.type || !typeof(IDefinition).IsAssignableFrom(model))
                throw new InvalidDataException($"Unexpected definition type {header.type} for {model.Name}.");
            if (header.schemaVersion != CurrentVersion)
                throw new InvalidDataException($"Definition {header.id} must be migrated before runtime use.");
            data.Remove("references");
            data.Remove("id");
            data.Remove("schemaVersion");
            data.Remove("type");
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            });
            var value = (IDefinition)data.ToObject(model, serializer);
            value.Validate();
            return value;
        }
    }
}
