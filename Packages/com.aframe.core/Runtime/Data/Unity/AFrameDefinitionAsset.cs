using UnityEngine;

namespace AFrame.Data
{
    [CreateAssetMenu(menuName = "AFrame/Data Definition", fileName = "Definition")]
    public sealed class AFrameDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string definitionId;
        [SerializeField] private int schemaVersion;
        [SerializeField] private string definitionType;
        [SerializeField] private string sourcePath;
        [SerializeField] private string sourceHash;
        [SerializeField] private string generatorVersion;
        [TextArea(3, 20)]
        [SerializeField] private string sourceJson;

        public string DefinitionId => definitionId;
        public int SchemaVersion => schemaVersion;
        public string DefinitionType => definitionType;
        public string SourcePath => sourcePath;
        public string SourceHash => sourceHash;
        public string GeneratorVersion => generatorVersion;
        public string SourceJson => sourceJson;

        public bool IsCurrent(string json)
        {
            return string.Equals(sourceHash, DefinitionSourceReader.ComputeHash(json), System.StringComparison.Ordinal);
        }

        public void SetSource(
            DefinitionDocument document,
            string json,
            string path,
            string hash,
            string version = "2.2.0")
        {
            definitionId = document.id;
            schemaVersion = document.schemaVersion;
            definitionType = document.type;
            sourceJson = json;
            sourcePath = path;
            sourceHash = hash;
            generatorVersion = version;
        }
    }
}
