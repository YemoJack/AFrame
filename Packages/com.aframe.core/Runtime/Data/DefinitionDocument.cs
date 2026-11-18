using System;

namespace AFrame.Data
{
    [Serializable]
    public sealed class DefinitionDocument
    {
        public string id;
        public int schemaVersion = 1;
        public string type;
    }

    [Serializable]
    public sealed class FeatureManifest
    {
        public string id;
        public string version = "0.1.0";
        public string[] dependencies = Array.Empty<string>();
        public string entryPoint;
        public string[] sourceRoots = Array.Empty<string>();
        public string[] definitionSources = Array.Empty<string>();
        public string[] acceptanceChecks = Array.Empty<string>();
    }
}

namespace AFrame.Data
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
    public sealed class DefinitionTypeAttribute : System.Attribute
    {
        public DefinitionTypeAttribute(string id) => Id = id;
        public string Id { get; }
    }

    public interface IDefinition
    {
        void Validate();
    }
}
