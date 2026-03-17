#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Tools
{
    public sealed class AbilityKitNewUnityProjectWindow : EditorWindow
    {
        private const string MenuPath = "Tools/AbilityKit/Dev/New Unity Project";

        private const string PrefTargetDir = "AbilityKit.NewUnityProject.TargetDirectory";
        private const string PrefProjectName = "AbilityKit.NewUnityProject.ProjectName";
        private const string PrefProfile = "AbilityKit.NewUnityProject.Profile";
        private const string PrefLinkOdin = "AbilityKit.NewUnityProject.LinkOdin";
        private const string PrefCopyUnsafe = "AbilityKit.NewUnityProject.CopyUnsafe";
        private const string PrefForce = "AbilityKit.NewUnityProject.Force";
        private const string PrefLinkOnly = "AbilityKit.NewUnityProject.LinkOnly";

        [Serializable]
        private sealed class ProfileJson
        {
            public string name;
            public string description;
            public string[] packages;
        }

        private string _targetDirectory;
        private string _projectName;
        private bool _linkOdin;
        private bool _copyUnsafe;
        private bool _force;
        private bool _linkOnly;

        private ProfileJson[] _profiles = Array.Empty<ProfileJson>();
        private string[] _profileDisplay = Array.Empty<string>();
        private int _selectedProfileIndex;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<AbilityKitNewUnityProjectWindow>(utility: false, title: "New Unity Project");
        }

        private void OnEnable()
        {
            _targetDirectory = EditorPrefs.GetString(PrefTargetDir, string.Empty);
            _projectName = EditorPrefs.GetString(PrefProjectName, "AbilityKit.Sandbox");
            _linkOdin = EditorPrefs.GetBool(PrefLinkOdin, true);
            _copyUnsafe = EditorPrefs.GetBool(PrefCopyUnsafe, true);
            _force = EditorPrefs.GetBool(PrefForce, false);
            _linkOnly = EditorPrefs.GetBool(PrefLinkOnly, false);

            ReloadProfiles();

            var lastProfile = EditorPrefs.GetString(PrefProfile, string.Empty);
            if (!string.IsNullOrEmpty(lastProfile))
            {
                var idx = Array.FindIndex(_profiles, p => string.Equals(p.name, lastProfile, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) _selectedProfileIndex = idx;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope())
            {
                DrawTargetDirectory();
                DrawProjectName();
                DrawProfile();
                DrawOptions();

                EditorGUILayout.Space(8);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("刷新 Profiles", GUILayout.Height(28)))
                    {
                        ReloadProfiles();
                    }

                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(!CanRun()))
                    {
                        if (GUILayout.Button("执行", GUILayout.Width(120), GUILayout.Height(28)))
                        {
                            Run();
                        }
                    }
                }

                EditorGUILayout.Space(6);

                EditorGUILayout.HelpBox(
                    "执行后会调用 Unity/Tools/AbilityKit.NewUnityProject.ps1。\n" +
                    "建议先关闭目标工程（如果已打开）。\n" +
                    "输出请看 Console。",
                    MessageType.Info
                );
            }
        }

        private void DrawTargetDirectory()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _targetDirectory = EditorGUILayout.TextField("目标目录", _targetDirectory ?? string.Empty);
                if (GUILayout.Button("选择...", GUILayout.Width(80)))
                {
                    var picked = EditorUtility.OpenFolderPanel("选择目标目录", string.IsNullOrEmpty(_targetDirectory) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : _targetDirectory, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _targetDirectory = picked;
                        EditorPrefs.SetString(PrefTargetDir, _targetDirectory);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_targetDirectory) && !Directory.Exists(_targetDirectory))
            {
                EditorGUILayout.HelpBox("目标目录不存在。", MessageType.Warning);
            }
        }

        private void DrawProjectName()
        {
            _projectName = EditorGUILayout.TextField("工程名", _projectName ?? string.Empty);
            EditorPrefs.SetString(PrefProjectName, _projectName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(_projectName))
            {
                EditorGUILayout.HelpBox("工程名不能为空。", MessageType.Warning);
            }
            else if (_projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                EditorGUILayout.HelpBox("工程名包含非法字符。", MessageType.Warning);
            }
        }

        private void DrawProfile()
        {
            if (_profiles.Length == 0)
            {
                EditorGUILayout.HelpBox("未找到 Profiles（Unity/Tools/Profiles/*.json）。", MessageType.Error);
                return;
            }

            _selectedProfileIndex = EditorGUILayout.Popup("Profile", _selectedProfileIndex, _profileDisplay);
            _selectedProfileIndex = Mathf.Clamp(_selectedProfileIndex, 0, Mathf.Max(0, _profiles.Length - 1));

            var p = _profiles[_selectedProfileIndex];
            EditorPrefs.SetString(PrefProfile, p.name ?? string.Empty);

            if (!string.IsNullOrEmpty(p.description))
            {
                EditorGUILayout.HelpBox(p.description, MessageType.None);
            }
        }

        private void DrawOptions()
        {
            _linkOdin = EditorGUILayout.ToggleLeft("链接 Odin（Sirenix）插件", _linkOdin);
            _copyUnsafe = EditorGUILayout.ToggleLeft("复制 Unsafe.dll（修复 MemoryPack 编译缺失）", _copyUnsafe);
            _force = EditorGUILayout.ToggleLeft("Force（覆盖已存在目录）", _force);
            _linkOnly = EditorGUILayout.ToggleLeft("LinkOnly（不创建骨架，仅链接 Packages）", _linkOnly);

            EditorPrefs.SetBool(PrefLinkOdin, _linkOdin);
            EditorPrefs.SetBool(PrefCopyUnsafe, _copyUnsafe);
            EditorPrefs.SetBool(PrefForce, _force);
            EditorPrefs.SetBool(PrefLinkOnly, _linkOnly);
        }

        private bool CanRun()
        {
            if (string.IsNullOrWhiteSpace(_targetDirectory) || !Directory.Exists(_targetDirectory)) return false;
            if (string.IsNullOrWhiteSpace(_projectName)) return false;
            if (_projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            if (_profiles.Length == 0) return false;
            return true;
        }

        private void ReloadProfiles()
        {
            try
            {
                var profilesDir = GetProfilesDirectory();
                if (!Directory.Exists(profilesDir))
                {
                    _profiles = Array.Empty<ProfileJson>();
                    _profileDisplay = Array.Empty<string>();
                    _selectedProfileIndex = 0;
                    return;
                }

                var files = Directory.GetFiles(profilesDir, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileNameWithoutExtension)
                    .ToArray();

                _profiles = files
                    .Select(f =>
                    {
                        var json = File.ReadAllText(f);
                        var p = JsonUtility.FromJson<ProfileJson>(json) ?? new ProfileJson();
                        if (string.IsNullOrEmpty(p.name))
                        {
                            p.name = Path.GetFileNameWithoutExtension(f);
                        }
                        return p;
                    })
                    .ToArray();

                _profileDisplay = _profiles
                    .Select(p => string.IsNullOrEmpty(p.description) ? (p.name ?? string.Empty) : $"{p.name} - {p.description}")
                    .ToArray();

                _selectedProfileIndex = Mathf.Clamp(_selectedProfileIndex, 0, Mathf.Max(0, _profiles.Length - 1));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                _profiles = Array.Empty<ProfileJson>();
                _profileDisplay = Array.Empty<string>();
                _selectedProfileIndex = 0;
            }
        }

        private void Run()
        {
            var profile = _profiles[_selectedProfileIndex];
            var profileName = profile.name ?? string.Empty;

            EditorPrefs.SetString(PrefTargetDir, _targetDirectory ?? string.Empty);
            EditorPrefs.SetString(PrefProjectName, _projectName ?? string.Empty);
            EditorPrefs.SetString(PrefProfile, profileName);

            var scriptPath = GetNewProjectScriptPath();
            if (!File.Exists(scriptPath))
            {
                EditorUtility.DisplayDialog("错误", "找不到脚本：\n" + scriptPath, "OK");
                return;
            }

            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -TargetDirectory \"{_targetDirectory}\" -ProjectName \"{_projectName}\" -Profile \"{profileName}\"";
            if (_linkOdin) args += " -LinkOdin";
            if (_copyUnsafe) args += " -CopyUnsafe";
            if (_force) args += " -Force";
            if (_linkOnly) args += " -LinkOnly";

            try
            {
                EditorUtility.DisplayProgressBar("AbilityKit", "正在执行脚本...", 0.3f);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = GetRepoUnityDirectory(),
                };

                using var p = new Process { StartInfo = psi };
                p.Start();
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    UnityEngine.Debug.Log(stdout);
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    UnityEngine.Debug.LogError(stderr);
                }

                if (p.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("完成", "已执行完成。请到目标目录用 Unity Hub 打开新工程。", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("失败", $"脚本退出码：{p.ExitCode}\n请看 Console 输出。", "OK");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                EditorUtility.DisplayDialog("异常", e.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string GetRepoUnityDirectory()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string GetToolsDirectory()
        {
            return Path.Combine(GetRepoUnityDirectory(), "Tools");
        }

        private static string GetProfilesDirectory()
        {
            return Path.Combine(GetToolsDirectory(), "Profiles");
        }

        private static string GetNewProjectScriptPath()
        {
            return Path.Combine(GetToolsDirectory(), "AbilityKit.NewUnityProject.ps1");
        }
    }
}
#endif
