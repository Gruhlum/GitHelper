using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HexTecGames.Basics;
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
            string jsonText = File.ReadAllText(path + "\\package.json");

            //string name = GetJsonValue(jsonText, "name");
            currentVersion = new VersionNumber(GetJsonValue(jsonText, "version"));
            nextVersion = currentVersion.GetIncreasedVersion(UpdateType.Minor);
            displayName = GetJsonValue(jsonText, "displayName");
            modifiedFiles = GetModifiedFileNames(path);
            changeText = string.Join(Environment.NewLine, modifiedFiles);
            shortStats = GetShortStats(path);
        }

        private string GetShortStats(string path)
        {
            string diff = "git diff HEAD --shortstat";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c cd {path} && {diff}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var cmdProcess = Process.Start(psi);
            string output = cmdProcess.StandardOutput.ReadToEnd();
            cmdProcess.WaitForExit();
            return output.Trim().Split('\n').Last();
        }

        private string Run()
        {
            IncreasePackageVersion(fullPath);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c cd {fullPath} && git add . && git commit -m \"{commitMessage}\" && git push",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var cmdProcess = Process.Start(psi);
            string output = cmdProcess.StandardOutput.ReadToEnd();
            cmdProcess.WaitForExit();
            string keyWord = "Enumerating objects:";
            int startIndex = output.IndexOf(keyWord) + keyWord.Length;

            Debug.Log(output);

            return output.Substring(startIndex);
        }

        private void IncreasePackageVersion(string path)
        {
            string fullFilePath = path + "\\package.json";
            string jsonText = File.ReadAllText(fullFilePath);
            // "version": "4.1.6"
            jsonText = jsonText.Replace($"\"version\": \"{currentVersion.ToString()}\"", $"\"version\": \"{nextVersion.ToString()}\"");
            File.WriteAllText(fullFilePath, jsonText);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                EditorGUILayout.LabelField("No Path!");
                return;
            }

            EditorGUILayout.LabelField("Package:", displayName);
            EditorGUILayout.LabelField("Current Version:", currentVersion.ToString());
            EditorGUILayout.LabelField("Next Version:", nextVersion.ToString());

            int currentIndex = GUILayout.Toolbar(selectedIndex, Enum.GetNames(typeof(UpdateType)));
            if (currentIndex != selectedIndex)
            {
                selectedIndex = currentIndex;
                nextVersion = currentVersion.GetIncreasedVersion((UpdateType)currentIndex);
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
                if (GUILayout.Button("Start", GUILayout.Height(30)))
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

        private bool CheckIfComplete()
        {
            return packageIndex >= allPaths.Count - 1;
        }
        private void SetupNextItem()
        {
            packageIndex++;
            Setup(allPaths[packageIndex]);
        }

        public List<string> GetModifiedFileNames(string path)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c cd {path} && git status",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var cmdProcess = Process.Start(psi);
            string output = cmdProcess.StandardOutput.ReadToEnd();
            cmdProcess.WaitForExit();


            string[] allLines = output.Trim().Split('\n');
            var results = allLines.Where(line => line.StartsWith("\t")).ToList();


            for (int i = results.Count - 1; i >= 0; i--)
            {
                string result = results[i];
                if (result.EndsWith(".cs.meta"))
                {
                    results.RemoveAt(i);
                    continue;
                }
                if (!result.StartsWith("\tdeleted") && !result.StartsWith("\tmodified"))
                {
                    result = "\tadded:" + result;
                }
                results[i] = result;
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