using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameMenuItems
    {
        [MenuItem("AFrame/Content/Import Definitions")]
        private static void ImportDefinitions()
        {
            AFrameDefinitionImporter.ImportAll();
        }

        [MenuItem("AFrame/Project/Validate Architecture")]
        private static void ValidateArchitecture()
        {
            var report = AFrameArchitectureValidator.Run();
            if (report.succeeded)
                Debug.Log("AFrame architecture validation succeeded.");
            else
                Debug.LogError("AFrame architecture validation found errors. See Assets/Game/Generated/ValidationReport.json.");
        }

        [MenuItem("AFrame/Project/Build Context")]
        private static void BuildContext()
        {
            AFrameContextBuilder.Build();
        }

        [MenuItem("AFrame/Project/Run Smoke Check")]
        private static void RunSmokeCheck()
        {
            AFrameDefinitionImporter.ImportAll(false);
            var report = AFrameArchitectureValidator.Run();
            AFrameContextBuilder.Build();
            if (report.succeeded)
                Debug.Log("AFrame smoke check succeeded.");
            else
                Debug.LogError("AFrame smoke check failed. See Assets/Game/Generated/ValidationReport.json.");
        }

        [MenuItem("AFrame/Project/Validate Task")]
        private static void ValidateTask()
        {
            var report = AFrameTaskValidator.ValidateFirstTask();
            if (report.succeeded)
                Debug.Log("AFrame task validation succeeded.");
            else
                Debug.LogError("AFrame task validation found errors. See Assets/Game/Generated/TaskValidationReport.json.");
        }
    }
}
