namespace EasySave.Core;

public class AppSettings
{
    public string BusinessSoftware { get; set; } = "";
    public List<string> EncryptedExtensions { get; set; } = new();
    public string EncryptionKey { get; set; } = "defaultkey";
    public string LogFormat { get; set; } = "JSON";
    public string Language { get; set; } = "EN";   
}