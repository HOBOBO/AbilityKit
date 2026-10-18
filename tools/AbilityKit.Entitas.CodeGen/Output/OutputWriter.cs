using System.Text;
using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Diagnostics;
using AbilityKit.Entitas.CodeGen.Pipeline;

namespace AbilityKit.Entitas.CodeGen.Output;

/// <summary>写盘器：按配置输出字节（EOL + BOM），可选清理陈旧生成文件（接入替换用）。</summary>
public static class OutputWriter
{
    public static void Write(string targetDir, FileSet files, CodeGenConfig config, IReadOnlySet<string> preservedFiles, IConsoleSink sink)
    {
        var emitBom = config.Output.Encoding == "utf8-bom";
        var newline = config.Output.LineEndings == "crlf" ? "\r\n" : "\n";
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);

        foreach (var file in files.Ordered)
        {
            var full = Path.Combine(targetDir, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, file.Content.Replace("\n", newline), encoding);
        }

        if (config.Output.CleanStale)
        {
            var keep = files.Ordered.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var p in preservedFiles) keep.Add(p);
            var stale = Directory.EnumerateFiles(targetDir, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(targetDir, f).Replace('\\', '/'))
                .Where(p => !keep.Contains(p));
            foreach (var path in stale)
            {
                var full = Path.Combine(targetDir, path);
                File.Delete(full);
                sink.Info($"[clean] 删除陈旧生成文件 {path}");
                var meta = full + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
            }
        }
    }
}
