using System;
using System.Collections;
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
        int selectedIndex = 1;
        string displayName;
        string fullPath;
        string commitMessage = "fixes";
        List<string> modifiedFiles = new List<string>();
        VersionNumber currentVersion;
        VersionNumber nextVersion;
        string helpText;

        string changeText;
        string shortStats;

        GUIStyle centerTextAlignmentStyle;

        private List<string> allPaths;
        private int packageIndex = 0;
        bool isComplete;

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
            EditorGUILayout.TextArea(commitMessage, GUILayout.Height(32));

            if (isComplete)
            {
                if (GUILayout.Button("Close", GUILayout.Height(30)))
                {
                    Close();
                }
            }
            else
            {
                if (GUILayout.Button("Add, Commit & Push", GUILayout.Height(30)))
                {
                    changeText = Run();
                    isComplete = CheckIfComplete();
                    if (!isComplete)
                    {
                        SetupNextItem();
                    }
                }
            }

            if (!string.IsNullOrEmpty(helpText))
            {
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(shortStats, centerTextAlignmentStyle);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(changeText, GUILayout.ExpandHeight(true));
        }
        public void Setup(List<string> paths)
        {
            allPaths = paths;
            packageIndex = 0;
            Setup(allPaths[packageIndex]);
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

            modifiedFiles = GetModifiedFileNames(path);
            changeText = string.Join(Environment.NewLine, modifiedFiles);
            shortStats = GetShortStats(path);
        }
        private void SetupNextItem()
        {
            selectedIndex = 1;
            packageIndex++;
            Setup(allPaths[packageIndex]);
        }

        private string Run()
        {
            if (currentVersion != null)
            {
                IncreasePackageVersion(fullPath);
            }

            // Run Git commands separately
            var lastDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(fullPath);
            RunGitCommand("add .");
            RunGitCommand($"commit -m \"{commitMessage}\"");
            string pushOutput = RunGitCommand("push");
            Directory.SetCurrentDirectory(lastDirectory);
            return pushOutput;
        }
        private string RunGitCommand(string arguments)
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

            using (var process = Process.Start(psi))
            {
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.Log(error);
                }
                return error;
            }
        }
        private void IncreasePackageVersion(string path)
        {
            string fullFilePath = path + "\\package.json";
            string jsonText = File.ReadAllText(fullFilePath);
            // "version": "4.1.6"
            jsonText = jsonText.Replace($"\"version\": \"{currentVersion.ToString()}\"", $"\"version\": \"{nextVersion.ToString()}\"");
            File.WriteAllText(fullFilePath, jsonText);
        }

        private string GetShortStats(string path)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff HEAD --shortstat",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = path
            };

            using (var gitProcess = Process.Start(psi))
            {
                string output = gitProcess.StandardOutput.ReadToEnd();
                string error = gitProcess.StandardError.ReadToEnd();
                gitProcess.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"Git error in '{path}': {error.Trim()}");
                }

                return output.Trim().Split('\n').LastOrDefault() ?? string.Empty;
            }
        }
        private bool CheckIfComplete()
        {
            return packageIndex >= allPaths.Count - 1;
        }

        public List<string> GetModifiedFileNames(string path)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = path
            };

            var results = new List<string>();

            using (var gitProcess = Process.Start(psi))
            {
                string output = gitProcess.StandardOutput.ReadToEnd();
                string error = gitProcess.StandardError.ReadToEnd();
                gitProcess.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"Git error in '{path}': {error.Trim()}");
                }

                var lines = output.Split('\n');

                foreach (var line in lines)
                {
                    if (line.Length < 3) continue;

                    string statusCode = line.Substring(0, 2);
                    if (statusCode == "??")
                    {
                        statusCode = " A";
                    }
                    string filePath = line.Substring(3);

                    if (filePath.EndsWith(".cs.meta")) continue;

                    string result = $"{statusCode}\t{filePath}";
                    results.Add(result);
                }
            }

            return results;
        }

        static string GetJsonValue(string json, string key)
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