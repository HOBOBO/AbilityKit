using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config;

namespace AbilityKit.Orleans.Grains.Gameplays.Moba.Resources;

/// <summary>
/// Loads MOBA runtime resources from the server deployment or the repository Resources directory.
/// </summary>
internal sealed class ServerMobaTextAssetLoader : ITextAssetLoader, ITextAssetDirectoryLoader
{
    private const string ResourceRootEnvironmentVariable = "ABILITYKIT_MOBA_RESOURCE_ROOT";
    private readonly IReadOnlyList<string> _resourceRoots;

    public ServerMobaTextAssetLoader()
        : this(FindResourceRoots())
    {
    }

    internal ServerMobaTextAssetLoader(IReadOnlyList<string> resourceRoots)
    {
        _resourceRoots = resourceRoots ?? throw new ArgumentNullException(nameof(resourceRoots));
    }

    public bool TryLoadText(string path, out string text)
    {
        text = null;
        if (!TryResolvePath(path, out var fullPath))
        {
            return false;
        }

        try
        {
            text = File.ReadAllText(fullPath);
            return !string.IsNullOrEmpty(text);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryLoadBytes(string path, out byte[] bytes)
    {
        bytes = null;
        if (!TryResolvePath(path, out var fullPath))
        {
            return false;
        }

        try
        {
            bytes = File.ReadAllBytes(fullPath);
            return bytes.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public IEnumerable<string> GetTextAssetPaths(string directory, string pattern)
    {
        var normalizedDirectory = NormalizePath(directory);
        if (string.IsNullOrEmpty(normalizedDirectory))
        {
            return Array.Empty<string>();
        }

        var searchPattern = string.IsNullOrWhiteSpace(pattern)
            ? "*.json"
            : pattern.Replace("**/", string.Empty).Replace("**", "*");
        var paths = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < _resourceRoots.Count; i++)
        {
            var root = _resourceRoots[i];
            var fullDirectory = Path.Combine(root, normalizedDirectory);
            if (!Directory.Exists(fullDirectory))
            {
                continue;
            }

            foreach (var fullPath in Directory.GetFiles(fullDirectory, searchPattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, fullPath)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                paths.Add(RemoveJsonExtension(relative));
            }
        }

        return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private bool TryResolvePath(string path, out string fullPath)
    {
        fullPath = string.Empty;
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        if (Path.IsPathRooted(normalizedPath))
        {
            return TryResolveFile(normalizedPath, out fullPath);
        }

        for (var i = 0; i < _resourceRoots.Count; i++)
        {
            if (TryResolveFile(Path.Combine(_resourceRoots[i], normalizedPath), out fullPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveFile(string path, out string fullPath)
    {
        fullPath = path;
        if (File.Exists(fullPath))
        {
            return true;
        }

        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            fullPath = path + ".json";
            return File.Exists(fullPath);
        }

        return false;
    }

    private static IReadOnlyList<string> FindResourceRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configuredRoot = Environment.GetEnvironmentVariable(ResourceRootEnvironmentVariable);
        AddExistingDirectory(roots, configuredRoot);

        AddResourceRoots(roots, AppContext.BaseDirectory);
        AddResourceRoots(roots, Directory.GetCurrentDirectory());
        return roots.ToArray();
    }

    private static void AddResourceRoots(ISet<string> roots, string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return;
        }

        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current != null)
        {
            AddUnityProjectResourceRoots(roots, Path.Combine(current.FullName, "Unity"));
            current = current.Parent;
        }
    }

    private static void AddUnityProjectResourceRoots(ISet<string> roots, string unityProjectRoot)
    {
        var assetsRoot = Path.Combine(unityProjectRoot, "Assets");
        var packagesRoot = Path.Combine(unityProjectRoot, "Packages");
        if (!Directory.Exists(assetsRoot) || !Directory.Exists(packagesRoot))
        {
            return;
        }

        AddExistingDirectory(roots, Path.Combine(assetsRoot, "Resources"));
        foreach (var resourceRoot in Directory.GetDirectories(packagesRoot, "Resources", SearchOption.AllDirectories))
        {
            AddExistingDirectory(roots, resourceRoot);
        }
    }

    private static void AddExistingDirectory(ISet<string> roots, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            roots.Add(Path.GetFullPath(path));
        }
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar);
    }

    private static string RemoveJsonExtension(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? path[..^5]
            : path;
    }
}
