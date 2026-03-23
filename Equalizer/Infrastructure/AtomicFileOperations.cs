using System.IO;

namespace Equalizer.Infrastructure;

internal static class AtomicFileOperations
{
    private const int WriteBufferSize = 65536;

    public static void Write(string targetPath, ReadOnlySpan<byte> data)
    {
        string tempPath = targetPath + ".tmp";

        using (var fs = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            WriteBufferSize,
            FileOptions.WriteThrough))
        {
            fs.Write(data);
            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(targetPath))
            File.Replace(tempPath, targetPath, targetPath + ".bak", ignoreMetadataErrors: true);
        else
            File.Move(tempPath, targetPath);
    }

    public static byte[]? ReadWithFallback(string targetPath)
    {
        if (TryRead(targetPath, out byte[]? primary) && primary!.Length > 0)
            return primary;

        string backupPath = targetPath + ".bak";
        if (TryRead(backupPath, out byte[]? backup) && backup!.Length > 0)
        {
            TryRestoreFromBackup(backupPath, targetPath);
            return backup;
        }

        return null;
    }

    private static bool TryRead(string path, out byte[]? data)
    {
        data = null;
        if (!File.Exists(path)) return false;
        try
        {
            data = File.ReadAllBytes(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryRestoreFromBackup(string backupPath, string targetPath)
    {
        try { File.Copy(backupPath, targetPath, overwrite: true); }
        catch { }
    }
}