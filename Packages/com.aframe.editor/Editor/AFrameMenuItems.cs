using UnityEditor;

namespace AFrame.Editor
{
    public static class AFrameMenuItems
    {
        [MenuItem("AFrame/Content/Import Definitions")]
        private static void ImportDefinitions() => AFrameDefinitionImporter.ImportAll();

        [MenuItem("AFrame/Project/Validate Architecture")]
        private static void ValidateArchitecture() => AFrameEditorCommands.Menu("validate");

        [MenuItem("AFrame/Project/Build Context")]
        private static void BuildContext() => AFrameEditorCommands.Menu("context");

        [MenuItem("AFrame/Project/Run Smoke Check")]
        private static void RunSmokeCheck() => AFrameEditorCommands.Menu("smoke");
    }
}
