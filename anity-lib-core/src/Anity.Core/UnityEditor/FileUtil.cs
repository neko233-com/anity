using System;
using System.IO;

namespace UnityEditor
{
    public static class FileUtil
    {
        public static void CopyFileOrDirectory(string source, string dest)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(dest)) return;

            try
            {
                if (Directory.Exists(source))
                {
                    CopyDirectory(source, dest, true);
                }
                else if (File.Exists(source))
                {
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.Copy(source, dest, true);
                }
            }
            catch { }
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool recursive)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destDir, fileName), true);
            }

            if (recursive)
            {
                foreach (var dir in Directory.GetDirectories(sourceDir))
                {
                    var dirName = Path.GetFileName(dir);
                    CopyDirectory(dir, Path.Combine(destDir, dirName), true);
                }
            }
        }

        public static void DeleteFileOrDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }

        public static bool ReplaceFile(string src, string dst)
        {
            try
            {
                if (File.Exists(dst)) File.Delete(dst);
                if (File.Exists(src))
                {
                    File.Move(src, dst);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static string GetUniqueTempPathInProject()
        {
            return Path.Combine("Temp", Guid.NewGuid().ToString("N"));
        }

        public static string GetProjectRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try
            {
                var projectRoot = Directory.GetCurrentDirectory();
                if (path.StartsWith(projectRoot, StringComparison.Ordinal))
                {
                    var relative = path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return relative.Replace('\\', '/');
                }
            }
            catch { }
            return path;
        }

        public static void CopyPathOrDirectory(string source, string dest)
        {
            CopyFileOrDirectory(source, dest);
        }
    }
}
