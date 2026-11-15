using System;

namespace AFrame.Editor
{
    [Serializable]
    public sealed class ValidationIssue
    {
        public string severity;
        public string path;
        public string message;
    }

    [Serializable]
    public sealed class ValidationReport
    {
        public string generatedAtUtc;
        public bool succeeded;
        public ValidationIssue[] issues = Array.Empty<ValidationIssue>();
    }

    [Serializable]
    internal sealed class ProjectMapDocument
    {
        public string projectName;
        public string unityVersion;
        public string[] packages = Array.Empty<string>();
        public string[] featureManifests = Array.Empty<string>();
        public string[] scripts = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class FeatureContractDocument
    {
        public FeatureContractSummary[] features = Array.Empty<FeatureContractSummary>();
    }

    [Serializable]
    internal sealed class FeatureContractSummary
    {
        public string id;
        public string path;
        public string entryPoint;
        public string[] dependencies = Array.Empty<string>();
        public string[] acceptanceChecks = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class ModuleGraphDocument
    {
        public ProfileModuleSummary[] profiles = Array.Empty<ProfileModuleSummary>();
    }

    [Serializable]
    internal sealed class ProfileModuleSummary
    {
        public string profile;
        public ModuleSummary[] modules = Array.Empty<ModuleSummary>();
    }

    [Serializable]
    internal sealed class ModuleSummary
    {
        public string id;
        public string[] dependencies = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class PublicApiIndexDocument
    {
        public PublicApiSummary[] entries = Array.Empty<PublicApiSummary>();
    }

    [Serializable]
    internal sealed class PublicApiSummary
    {
        public string assembly;
        public string path;
        public string[] types = Array.Empty<string>();
    }
}
