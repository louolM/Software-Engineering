using EasySave.Services.Interfaces;

namespace EasySave.Infrastructure;

// Provides file-system operations required by the backup engine.
//
// Concrete implementation of IFileService
// Abstracting file I/O behind the interface makes the backup logic testable:w without touching the real file system.
public class FileService : IFileService
{
    public IEnumerable<string> GetAllFiles(string directory)
        => Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

    public void CopyFile(string source, string target)
    {
        var dir = Path.GetDirectoryName(target) ?? "";
        
        // If any intermediate directories in the target path do not exist, they are created automatically before the copy takes place.
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.Copy(source, target, true);
    }
}