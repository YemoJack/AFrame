using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    [Serializable]
    internal sealed class AFrameTaskDefinition
    {
        public string id;
        public string goal;
        public string[] allowedPaths = Array.Empty<string>();
        public string[] readOnlyPaths = Array.Empty<string>();
        public string[] forbiddenPaths = Array.Empty<string>();
        public string[] acceptanceChecks = Array.Empty<string>();
        public string changeManifest;
    }

    public static class AFrameTaskValidator
    {
        public const string ReportPath = "Assets/Game/Generated/TaskValidationReport.json";

        public static ValidationReport ValidateFirstTask(bool writeReport = true)
        {
            var paths = Directory.Exists("Tasks")
                ? Directory.GetFiles("Tasks", "*.task.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            if (paths.Length == 0)
                return CreateReport(writeReport, new[] { Issue("error", "Tasks", "No task file was found.") });

            return Validate(paths.OrderBy(path => path, StringComparer.Ordinal).First(), writeReport);
        }

        public static ValidationReport Validate(string taskPath, bool writeReport = true)
        {
            var issues = new List<ValidationIssue>();
            AFrameTaskDefinition task = null;
            try
            {
                task = JsonUtility.FromJson<AFrameTaskDefinition>(File.ReadAllText(taskPath));
            }
            catch (Exception exception)
            {
                issues.Add(Issue("error", taskPath, $"Task file is invalid JSON: {exception.Message}"));
            }

            if (task == null || string.IsNullOrWhiteSpace(task.id) || string.IsNullOrWhiteSpace(task.goal))
            {
                issues.Add(Issue("error", taskPath, "Task requires id and goal."));
                return CreateReport(writeReport, issues);
            }

            if (task.allowedPaths == null || task.allowedPaths.Length == 0)
                issues.Add(Issue("error", taskPath, "Task must declare allowedPaths."));

            foreach (var changedPath in ReadChangedPaths())
            {
                if (MatchesAny(changedPath, task.forbiddenPaths))
                    issues.Add(Issue("error", changedPath, $"Path is forbidden by task {task.id}."));
                else if (!MatchesAny(changedPath, task.allowedPaths))
                    issues.Add(Issue("error", changedPath, $"Path is outside task {task.id} allowedPaths."));
                else if (MatchesAny(changedPath, task.readOnlyPaths))
                    issues.Add(Issue("error", changedPath, $"Path is read-only for task {task.id}."));
            }

            if (!string.IsNullOrWhiteSpace(task.changeManifest)
                && !File.Exists(task.changeManifest))
            {
                issues.Add(Issue("warning", task.changeManifest, "Task changeManifest has not been generated yet."));
            }

            return CreateReport(writeReport, issues);
        }

        private static IEnumerable<string> ReadChangedPaths()
        {
            foreach (var path in RunGit("diff --name-only HEAD"))
                yield return path;
            foreach (var path in RunGit("ls-files --others --exclude-standard"))
                yield return path;
        }

        private static IEnumerable<string> RunGit(string arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(start))
            {
                if (process == null)
                    yield break;
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                        yield return Normalize(line);
                }
                process.WaitForExit();
            }
        }

        private static bool MatchesAny(string path, IEnumerable<string> patterns)
        {
            return (patterns ?? Array.Empty<string>()).Any(pattern => Matches(path, pattern));
        }

        private static bool Matches(string path, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return false;
            var normalized = Normalize(pattern);
            if (normalized.EndsWith("/**", StringComparison.Ordinal))
                return path.StartsWith(normalized.Substring(0, normalized.Length - 2), StringComparison.Ordinal);
            return string.Equals(path, normalized, StringComparison.Ordinal);
        }

        private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/').TrimStart('.', '/');

        private static ValidationIssue Issue(string severity, string path, string message)
        {
            return new ValidationIssue { severity = severity, path = path, message = message };
        }

        private static ValidationReport CreateReport(bool writeReport, IEnumerable<ValidationIssue> issues)
        {
            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                issues = issues.ToArray()
            };
            report.succeeded = report.issues.All(issue => issue.severity != "error");

            if (writeReport)
            {
                Directory.CreateDirectory("Assets/Game/Generated");
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true) + Environment.NewLine);
                AssetDatabase.Refresh();
            }

            return report;
        }
    }
}
