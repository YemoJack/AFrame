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
        [TextArea(3, 20)]
        [SerializeField] private string sourceJson;

        public string DefinitionId => definitionId;
        public int SchemaVersion => schemaVersion;
        public string DefinitionType => definitionType;
        public string SourcePath => sourcePath;
        public string SourceHash => sourceHash;
        public string SourceJson => sourceJson;

        public void SetSource(
            DefinitionDocument document,
            string json,
            string path,
            string hash)
        {
            definitionId = document.id;
            schemaVersion = document.schemaVersion;
            definitionType = document.type;
            sourceJson = json;
            sourcePath = path;
            sourceHash = hash;
        }
    }
}
