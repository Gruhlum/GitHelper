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
    public class GitHelperWindow : EditorWindow
    {
        [SerializeField] private string directoryPath;

        private List<string> folderPaths = new List<string>();
        private List<string> hasChangesPaths = new List<string>();

        private string helpText;
        private string changedPackageNames;

        private Vector2 scrollPos;
        private const string PATH_SAVE_KEY = "UNITY_PROJECTS_PATH";
        private const string DEFAULT_PATH = "C:\\Users\\NAME\\Documents\\Projects\\Unity";

        [MenuItem("Tools/Git Helper")]
        public static void ShowWindow()
        {
            GetWindow(typeof(GitHelperWindow));
        }

        private void OnEnable()
        {
            directoryPath = EditorPrefs.GetString(PATH_SAVE_KEY, DEFAULT_PATH);
            if (directoryPath == DEFAULT_PATH)
            {
                return;
            }
            titleContent = new GUIContent("Git Helper");
            GetSubFolders();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PATH_SAVE_KEY, directoryPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Path");
            directoryPath = EditorGUILayout.TextField(label: string.Empty, directoryPath);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Total Repositories:", folderPaths.Count.ToString());
            EditorGUILayout.LabelField("With Changes:", hasChangesPaths.Count.ToString());

            EditorGUILayout.Space();

            //automate = EditorGUILayout.Toggle("Automate", automate);

            EditorGUILayout.Space();

            if (GUILayout.Button("Check", GUILayout.Height(30)))
            {
                GetSubFolders();
            }

            if (folderPaths.Count > 0 && hasChangesPaths.Count > 0)
            {
                if (GUILayout.Button("Start", GUILayout.Height(30)))
                {
                    GetSubFolders();
                    GitItemWindow result = GetWindow(typeof(GitItemWindow)) as GitItemWindow;
                    result.Setup(hasChangesPaths);
                }
            }
            if (!string.IsNullOrEmpty(helpText))
            {
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }

            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
            textAreaStyle.wordWrap = false;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            GUILayout.TextArea(changedPackageNames, textAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        private List<string> GetFilteredDirectories(string rootPath)
        {
            List<string> result = new List<string>();

            void Search(string currentPath)
            {
                // Check if this folder contains a .git subfolder
                if (Directory.GetDirectories(currentPath).Any(d =>
                    Path.GetFileName(d).Equals(".git", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(currentPath);
                    return; // Stop exploring deeper into this folder
                }

                // Otherwise, recurse into each subfolder
                foreach (string subDir in Directory.GetDirectories(currentPath))
                {
                    Search(subDir);
                }
            }

            Search(rootPath);
            return result;
        }

        private void GetSubFolders()
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                return;
            }

            folderPaths = GetFilteredDirectories(directoryPath);

            hasChangesPaths = new List<string>();
            List<string> packageNamesWithChanges = new List<string>();
            foreach (string path in folderPaths)
            {
                if (HasChanges(path))
                {
                    hasChangesPaths.Add(path);
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    packageNamesWithChanges.Add(dirInfo.Name);
                }
            }
            changedPackageNames = string.Join(Environment.NewLine, packageNamesWithChanges);
        }

        private bool HasChanges(string path)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = path
            };

            using (Process gitProcess = Process.Start(psi))
            {
                string output = gitProcess.StandardOutput.ReadToEnd();
                string error = gitProcess.StandardError.ReadToEnd();
                gitProcess.WaitForExit();

                // Optionally check error output for diagnostics
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"Git error in '{path}': {error.Trim()}");
                }

                return !string.IsNullOrWhiteSpace(output); // If there's output, the working tree has changes
            }
        }
    }
}