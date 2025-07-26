using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using HexTecGames.Basics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HexTecGames.Basics.Editor
{
    public class GitHelperWindow : EditorWindow
    {
        [SerializeField] private string rootPath = "C:\\Users\\Patrick\\Documents\\Projects\\Unity";

        private List<string> folderPaths = new List<string>();
        private List<string> hasChangesPaths = new List<string>();

        private string helpText;
        private string changedPackageNames;

        [MenuItem("Tools/Git Helper")]
        public static void ShowWindow()
        {
            GetWindow(typeof(GitHelperWindow));
        }

        private void OnEnable()
        {
            GetSubFolders();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Path");
            rootPath = EditorGUILayout.TextField(label: string.Empty, rootPath);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Total Packages:", folderPaths.Count.ToString());
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
                    var result = GetWindow(typeof(GitItemWindow)) as GitItemWindow;
                    result.Setup(hasChangesPaths);
                }
            }
            if (!string.IsNullOrEmpty(helpText))
            {
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }

            EditorGUILayout.LabelField(changedPackageNames, GUILayout.ExpandHeight(true));
        }

        private bool IsGitRepo(string path)
        {
            var subDirs = Directory.GetDirectories(path);
            foreach (var subDir in subDirs)
            {
                var dirInfo = new DirectoryInfo(subDir);
                if (dirInfo.Name == ".git")
                {
                    return true;
                }
            }
            return false;
        }

        List<string> GetFilteredDirectories(string rootPath)
        {
            var result = new List<string>();

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
                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    Search(subDir);
                }
            }

            Search(rootPath);
            return result;
        }

        private void GetSubFolders()
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return;
            }

            folderPaths = GetFilteredDirectories(rootPath);

            hasChangesPaths = new List<string>();
            List<string> packageNamesWithChanges = new List<string>();
            foreach (var path in folderPaths)
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

        //public string Run()
        //{
        //    if (hasChangesPaths.Count <= 0)
        //    {
        //        return $"{folderPaths.Count} files checked in {packageFolder}";
        //    }

        //    foreach (var path in hasChangesPaths)
        //    {
        //        StartCommit(path);
        //    }
        //    return $"{folderPaths.Count} files checked in {packageFolder}";
        //}

        //private void StartCommit(string path)
        //{
        //    ProcessStartInfo psi = new ProcessStartInfo
        //    {
        //        FileName = "cmd.exe",
        //        Arguments = $"/K \"cd /d {path} && git status\"",
        //        UseShellExecute = true,
        //        CreateNoWindow = false,
        //    };

        //    var cmdProcess = Process.Start(psi);
        //    cmdProcess.WaitForExit();
        //}

        private bool HasChanges(string path)
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
            string lastLine = output.Trim().Split('\n').Last();
            if (string.IsNullOrEmpty(lastLine))
            {
                return false;
            }
            //Debug.Log(path + " - " + lastLine);
            return lastLine != "nothing to commit, working tree clean";
        }
    }
}