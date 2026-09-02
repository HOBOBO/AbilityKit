#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;

namespace AbilityKit.Editor.Platform.Export
{
    public enum EditorAtomicWriteStatus
    {
        Written = 0,
        Unchanged = 1
    }

    public static class EditorAtomicFileWriter
    {
        public static EditorAtomicWriteStatus WriteAllText(
            string path,
            string content,
            Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export path must not be empty.", nameof(path));
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var absolutePath = Path.GetFullPath(path);
            encoding = encoding ?? new UTF8Encoding(false);
            if (File.Exists(absolutePath) &&
                string.Equals(File.ReadAllText(absolutePath, encoding), content, StringComparison.Ordinal))
            {
                return EditorAtomicWriteStatus.Unchanged;
            }

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = absolutePath + ".abilitykit.tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, content, encoding);
                if (File.Exists(absolutePath))
                {
                    try
                    {
                        File.Replace(temporaryPath, absolutePath, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceByMove(temporaryPath, absolutePath);
                    }
                    catch (IOException)
                    {
                        ReplaceByMove(temporaryPath, absolutePath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, absolutePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            return EditorAtomicWriteStatus.Written;
        }

        private static void ReplaceByMove(string temporaryPath, string destinationPath)
        {
            var backupPath = destinationPath + ".abilitykit.bak." + Guid.NewGuid().ToString("N");
            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(temporaryPath, destinationPath);
                File.Delete(backupPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                    File.Move(backupPath, destinationPath);
                throw;
            }
            finally
            {
                if (File.Exists(backupPath) && File.Exists(destinationPath))
                    File.Delete(backupPath);
            }
        }
    }
}
#endif
