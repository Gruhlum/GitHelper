using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HexTecGames.Basics.Editor
{
    public class GitItemWindow : EditorWindow
    {
        private int selectedIndex = 1;
        private string displayName;
        private string branchName;
        private string fullPath;
        private string commitMessage = "fixes";
        private List<string> modifiedFiles = new List<string>();
        private VersionNumber currentVersion;
        private VersionNumber nextVersion;
        private string helpText;

        private string changeText;
        private string shortStats;

        private GUIStyle centerTextAlignmentStyle;

        private List<string> allPaths;
        private int repoIndex = 0;
        private Vector2 scrollPos;

        private void OnEnable()
        {
            centerTextAlignmentStyle = new GUIStyle();
            centerTextAlignmentStyle.normal.textColor = Color.white;
            centerTextAlignmentStyle.alignment = TextAnchor.MiddleCenter;
            titleContent = new GUIContent("Git Action");
        }
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                EditorGUILayout.LabelField("No Path!");
                return;
            }

            EditorGUILayout.LabelField("Name:", displayName);
            EditorGUILayout.LabelField("Branch:", branchName);

            if (currentVersion != null && nextVersion != null)
            {
                EditorGUILayout.LabelField("Current Version:", currentVersion.ToString());
                EditorGUILayout.LabelField("Next Version:", nextVersion.ToString());

                int currentIndex = GUILayout.Toolbar(selectedIndex, Enum.GetNames(typeof(UpdateType)));
                if (currentIndex != selectedIndex)
                {
                    selectedIndex = currentIndex;
                    nextVersion = currentVersion.GetIncreasedVersion((UpdateType)currentIndex);
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commit Message");
            var commitInput = EditorGUILayout.TextArea(commitMessage, GUILayout.Height(36));

            if (GUILayout.Button("Add, Commit & Push", GUILayout.Height(30)))
            {
                Run();
                AdvanceItem();
            }
            else commitMessage = commitInput;

            if (GUILayout.Button("Skip", GUILayout.Height(30)))
            {
                AdvanceItem();
            }

            if (!string.IsNullOrEmpty(helpText))
            {
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(shortStats, centerTextAlignmentStyle);
            EditorGUILayout.Space();

            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
            textAreaStyle.wordWrap = false;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            GUILayout.TextArea(changeText, textAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

        }

        private void AdvanceItem()
        {
            if (CheckIfComplete())
            {
                Close();
                return;
            }
            else SetupNextItem();
        }

        public void Setup(List<string> paths)
        {
            allPaths = paths;
            repoIndex = 0;
            Setup(allPaths[repoIndex]);
        }
        private void Setup(string path)
        {
            fullPath = path;

            string packagePath = path + "\\package.json";

            if (File.Exists(packagePath))
            {
                string jsonText = File.ReadAllText(packagePath);
                currentVersion = new VersionNumber(GetJsonValue(jsonText, "version"));
                nextVersion = currentVersion.GetIncreasedVersion(UpdateType.Minor);
                displayName = GetJsonValue(jsonText, "displayName");
            }
            else displayName = new DirectoryInfo(path).Name;

            branchName = GetCurrentBranch(path);
            modifiedFiles = GetModifiedFileNames(path);
            changeText = string.Join(Environment.NewLine, modifiedFiles);
            shortStats = GetShortStats(path);
        }
        private void SetupNextItem()
        {
            selectedIndex = 1;
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;
            commitMessage = "fixes";
            repoIndex++;
            Setup(allPaths[repoIndex]);
        }

        private string Run()
        {
            if (currentVersion != null)
            {
                IncreasePackageVersion(fullPath);
            }

            // Run Git commands separately
            string lastDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(fullPath);
            RunGitCommand("add .");
            RunGitCommand($"commit -m \"{commitMessage}\"");
            string pushOutput = RunGitCommand("push");
            Directory.SetCurrentDirectory(lastDirectory);
            return pushOutput;
        }
       
        private void IncreasePackageVersion(string path)
        {
            string fullFilePath = path + "\\package.json";
            string jsonText = File.ReadAllText(fullFilePath);
            // "version": "4.1.6"
            jsonText = jsonText.Replace($"\"version\": \"{currentVersion}\"", $"\"version\": \"{nextVersion}\"");
            File.WriteAllText(fullFilePath, jsonText);
        }

        private bool CheckIfComplete()
        {
            return repoIndex >= allPaths.Count - 1;
        }
        private static string RunGitCommand(string arguments, string workingDirectory = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                psi.WorkingDirectory = workingDirectory;
            }

            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"Git error: {error.Trim()}");
                }

                return output.Trim();
            }
        }

        public static string GetShortStats(string path)
        {
            string output = RunGitCommand("diff HEAD --shortstat", path);
            return output.Split('\n').LastOrDefault() ?? string.Empty;
        }

        public static string GetCurrentBranch(string path)
        {
            string output = RunGitCommand("rev-parse --abbrev-ref HEAD", path);
            return string.IsNullOrWhiteSpace(output) ? "(unknown branch)" : output;
        }

        public static List<string> GetModifiedFileNames(string path)
        {
            string output = RunGitCommand("status --porcelain", path);
            var results = new List<string>();

            foreach(string line in output.Split('\n'))
{
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string statusCode = parts[0];
                    string filePath = parts[1];
                    results.Add($"{statusCode}\t{filePath}");
                }
            }

            return results;
        }

        private static string GetJsonValue(string json, string key)
        {
            string search = $"\"{key}\":";
            int index = json.IndexOf(search);
            if (index == -1)
                return null;

            index += search.Length;

            // Skip whitespace
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;

            // Handle value wrapped in quotes
            if (json[index] == '"')
            {
                int start = ++index;
                int end = json.IndexOf('"', start);
                return json.Substring(start, end - start);
            }

            // Handle non-quoted value (e.g., numbers, booleans)
            int endIndex = json.IndexOfAny(new char[] { ',', '\n', '}' }, index);
            return json.Substring(index, endIndex - index).Trim();
        }
    }
}