using EasySave.Core;

namespace EasySave.Infrastructure;

public class FileService : IFileService
{
    public IEnumerable<string> GetAllFiles(string directory)
    {
        return Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
    }

    public void CopyFile(string source, string target)
    {
        var dir = Path.GetDirectoryName(target) ?? "";

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.Copy(source, target, true);
    }
}