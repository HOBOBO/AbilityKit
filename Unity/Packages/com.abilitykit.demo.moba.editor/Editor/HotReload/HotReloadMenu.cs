using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.HotReload;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Game.Battle;
using UnityEditor;

namespace AbilityKit.Game.Editor.HotReload
{
    public static class HotReloadMenu
    {
        private const string HotfixProjectRelative = "HotUpdateSrc/Hotfix.Ability.Moba/Hotfix.Ability.Moba.csproj";
        private const string OutputDirRelative = "Library/HotUpdate";

        [MenuItem("Tools/AbilityKit/Hot Reload/Compile Hotfix")]
        public static void CompileHotfix()
        {
            var unityDir = GetUnityProjectDir();
            var csproj = Path.Combine(unityDir, HotfixProjectRelative);
            var outDir = Path.Combine(unityDir, OutputDirRelative);

            if (!File.Exists(csproj))
            {
                UnityEngine.Debug.LogError($"Hotfix csproj not found: {csproj}");
                return;
            }

            Directory.CreateDirectory(outDir);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csproj}\" -c Debug -o \"{outDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var p = Process.Start(psi);
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            UnityEngine.Debug.Log(stdout);
            if (!string.IsNullOrEmpty(stderr))
            {
                UnityEngine.Debug.LogError(stderr);
            }
        }

        [MenuItem("Tools/AbilityKit/Hot Reload/Reload Hotfix")]
        public static void ReloadHotfix()
        {
            var unityDir = GetUnityProjectDir();
            var outDir = Path.Combine(unityDir, OutputDirRelative);

            var dll = Directory.Exists(outDir)
                ? Directory.GetFiles(outDir, "Hotfix.Ability.Moba*.dll").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;

            if (string.IsNullOrEmpty(dll) || !File.Exists(dll))
            {
                UnityEngine.Debug.LogError($"Hotfix dll not found in {outDir}. Please Compile Hotfix first.");
                return;
            }

            var pdb = Path.ChangeExtension(dll, ".pdb");

            var dllBytes = File.ReadAllBytes(dll);
            byte[] pdbBytes = null;
            if (File.Exists(pdb)) pdbBytes = File.ReadAllBytes(pdb);

            Assembly asm;
            try
            {
                asm = pdbBytes != null ? Assembly.Load(dllBytes, pdbBytes) : Assembly.Load(dllBytes);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
                return;
            }

            var entryType = asm.GetTypes().FirstOrDefault(t => typeof(IHotfixEntry).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);
            if (entryType == null)
            {
                UnityEngine.Debug.LogError($"No IHotfixEntry found in {dll}");
                return;
            }

            IHotfixEntry entry;
            try
            {
                entry = (IHotfixEntry)Activator.CreateInstance(entryType);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
                return;
            }

            // Provide a logger override for hotfix layer
            var logger = new UnityHotfixLogger();

            var session = BattleLogicSessionHost.Current;
            if (session == null)
            {
                UnityEngine.Debug.LogError("No BattleLogicSessionHost.Current");
                return;
            }

            if (!session.TryGetWorld(out var world) || world == null)
            {
                UnityEngine.Debug.LogError("Session has no world");
                return;
            }

            if (!(world is IEntitasWorld ew))
            {
                UnityEngine.Debug.LogError($"World is not IEntitasWorld: {world.GetType().FullName}");
                return;
            }

            // Apply to current battle world
            if (!HotReloadRuntime.Apply(ew, new EntryWithLogger(entry, logger), out var err))
            {
                UnityEngine.Debug.LogError(err);
                return;
            }

            UnityEngine.Debug.Log($"Hotfix reloaded: {entry.Name} ({Path.GetFileName(dll)})");
        }

        private static string GetUnityProjectDir()
        {
            // Application.dataPath points to <project>/Assets
            var assetsPath = UnityEngine.Application.dataPath;
            return Directory.GetParent(assetsPath).FullName;
        }

        private sealed class EntryWithLogger : IHotfixEntry
        {
            private readonly IHotfixEntry _inner;
            private readonly IHotfixLogger _logger;

            public EntryWithLogger(IHotfixEntry inner, IHotfixLogger logger)
            {
                _inner = inner;
                _logger = logger;
            }

            public string Name => _inner.Name;

            public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, AbilityKit.Ability.World.DI.IWorldResolver services)
            {
                if (services is HotfixServiceOverlay overlay)
                {
                    overlay.Set(typeof(IHotfixLogger), _logger);
                }
                _inner.Install(contexts, systems, services);
            }

            public void Uninstall(global::Entitas.IContexts contexts, global::Entitas.Systems systems, AbilityKit.Ability.World.DI.IWorldResolver services)
            {
                _inner.Uninstall(contexts, systems, services);
            }
        }
    }
}
