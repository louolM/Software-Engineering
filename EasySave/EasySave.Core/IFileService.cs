namespace EasySave.Core;

public interface IFileService
{
    IEnumerable<string> GetAllFiles(string directory);
    void CopyFile(string source, string target);
}