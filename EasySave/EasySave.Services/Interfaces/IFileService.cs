namespace EasySave.Services.Interfaces;

// Defines the file-system operations required by the backup engine.
//
// Abstracting these operations behind an interface decouples the backup logic from the real file system, making it straightforward to substitute a mock implementation during unit testing.
public interface IFileService
{
    IEnumerable<string> GetAllFiles(string directory);
    void CopyFile(string source, string target);
}