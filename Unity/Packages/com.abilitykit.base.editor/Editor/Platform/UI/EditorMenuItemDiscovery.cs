#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace AbilityKit.Editor.Platform.UI
{
    /// <summary>
    /// A menu item path discovered by scanning loaded assemblies for <c>[MenuItem]</c>
    /// declarations. The path alone is enough to reopen the window through
    /// <c>EditorApplication.ExecuteMenuItem</c>, so no <see cref="MethodInfo"/> is retained.
    /// </summary>
    public readonly struct EditorMenuItemInfo
    {
        public EditorMenuItemInfo(string path, int priority)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Priority = priority;
        }

        public string Path { get; }
        public int Priority { get; }
    }

    /// <summary>
    /// Reflectively discovers AbilityKit <c>[MenuItem]</c> declarations that are not yet
    /// registered through the Editor Platform, so the Hub can surface them as a migration
    /// backlog. Discovery is deliberately exception-isolated: a hostile or dynamic assembly
    /// must never abort the scan.
    /// </summary>
    public static class EditorMenuItemDiscovery
    {
        public const string WindowRoot = "Window/AbilityKit/";
        public const string ToolsRoot = "Tools/AbilityKit/";
        public const string AssetsRoot = "Assets/AbilityKit/";

        public static IReadOnlyList<string> DefaultRoots { get; } =
            new[] { WindowRoot, ToolsRoot, AssetsRoot };

        public static IReadOnlyList<EditorMenuItemInfo> Discover(
            IEnumerable<Assembly> assemblies,
            IEnumerable<string> roots)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            if (roots == null) throw new ArgumentNullException(nameof(roots));

            var rootList = roots
                .Select(NormalizeRoot)
                .Where(root => root.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (rootList.Length == 0) return Array.Empty<EditorMenuItemInfo>();

            var items = new List<EditorMenuItemInfo>();
            var seenMethods = new HashSet<MethodInfo>();
            foreach (var assembly in assemblies)
            {
                if (assembly == null) continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null) continue;
                foreach (var type in types)
                {
                    if (type == null) continue;

                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var method in methods)
                    {
                        if (method == null || !seenMethods.Add(method)) continue;

                        MenuItem[] attributes;
                        try
                        {
                            attributes = (MenuItem[])method.GetCustomAttributes(typeof(MenuItem), false);
                        }
                        catch
                        {
                            continue;
                        }

                        foreach (var attribute in attributes)
                        {
                            if (attribute == null || attribute.validate) continue;
                            var path = attribute.menuItem;
                            if (string.IsNullOrWhiteSpace(path)) continue;
                            if (!rootList.Any(root => path.StartsWith(root, StringComparison.Ordinal))) continue;

                            items.Add(new EditorMenuItemInfo(path, attribute.priority));
                        }
                    }
                }
            }

            return items
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Priority)
                .ToArray();
        }

        public static IReadOnlyList<EditorMenuItemInfo> Exclude(
            IEnumerable<EditorMenuItemInfo> items,
            IEnumerable<string> paths)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (paths == null) throw new ArgumentNullException(nameof(paths));

            var excluded = new HashSet<string>(
                paths.Where(path => !string.IsNullOrWhiteSpace(path)),
                StringComparer.Ordinal);
            return items.Where(item => !excluded.Contains(item.Path)).ToArray();
        }

        public static string StripRoot(string path, IEnumerable<string> roots)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (roots == null) return path;

            foreach (var root in roots)
            {
                var normalized = NormalizeRoot(root);
                if (normalized.Length > 0 && path.StartsWith(normalized, StringComparison.Ordinal))
                {
                    return path.Substring(normalized.Length);
                }
            }

            return path;
        }

        private static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return string.Empty;
            var normalized = root.Trim();
            return normalized.EndsWith("/", StringComparison.Ordinal)
                ? normalized
                : normalized + "/";
        }
    }
}
#endif
