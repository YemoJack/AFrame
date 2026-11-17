using System;

namespace AFrame.Editor
{
    [Serializable]
    public sealed class ValidationIssue
    {
        public string source;
        public string scope;
        public string severity;
        public string path;
        public string message;
    }

    [Serializable]
    public sealed class ValidationReport
    {
        public string generatedAtUtc;
        public bool succeeded;
        public string[] covered;
        public string[] checkedScopes;
        public string[] notCovered;
        public ValidationIssue[] issues = Array.Empty<ValidationIssue>();
    }

}
