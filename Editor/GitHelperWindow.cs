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
        [SerializeField] private string packageFolder = "C:\\Users\\Patrick\\Documents\\Projects\\Unity\\_Misc\\Packages";

        private List<string> folderPaths = new List<string>();
        private List<string> hasChangesPaths = new List<string>();

        private string helpText;


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
            packageFolder = EditorGUILayout.TextField(label: string.Empty, packageFolder);

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

        private void GetSubFolders()
        {
            if (string.IsNullOrEmpty(packageFolder))
            {
                return;
            }

            folderPaths = Directory.GetDirectories(packageFolder).ToList();

            for (int i = folderPaths.Count - 1; i >= 0; i--)
            {
                if (!IsGitRepo(folderPaths[i]))
                {
                    folderPaths.RemoveAt(i);
                }
            }

            hasChangesPaths = new List<string>();


            foreach (var path in folderPaths)
            {
                // Run cmd and check with git status if any changes are needed
                // If not, continue
                // Else run git add and open an input prompt where the user can enter a commit msg
                // Increment the version number
                // run git push
                if (HasChanges(path))
                {
                    hasChangesPaths.Add(path);
                }
            }
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