using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AFrame.Data
{
    public static class DefinitionSourceReader
    {
        public static string ComputeHash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static bool TryRead(
            string path,
            out DefinitionDocument document,
            out string json,
            out string error)
        {
            document = null;
            json = string.Empty;
            error = string.Empty;

            if (!File.Exists(path))
            {
                error = $"Definition source does not exist: {path}";
                return false;
            }

            json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Definition source is empty.";
                return false;
            }

            try
            {
                document = JsonUtility.FromJson<DefinitionDocument>(json);
            }
            catch (System.Exception exception)
            {
                error = $"Invalid JSON: {exception.Message}";
                return false;
            }

            if (document == null)
            {
                error = "JSON did not produce a definition document.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.id))
            {
                error = "Definition id is required.";
                return false;
            }

            if (document.schemaVersion <= 0)
            {
                error = "Definition schemaVersion must be greater than zero.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.type))
            {
                error = "Definition type is required.";
                return false;
            }

            return true;
        }
    }
}
