using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasySave.Core;
using EasySave.Services.Interfaces;

namespace EasySave.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepo;
    private CancellationTokenSource? _hideMsgCts;

    public event Action<string>? LanguageChanged;

    [ObservableProperty] private string _businessSoftware = string.Empty;
    [ObservableProperty] private string _encryptionKey = string.Empty;
    [ObservableProperty] private string _encryptedExtensions = string.Empty;
    [ObservableProperty] private bool _logFormatXml = false;
    [ObservableProperty] private bool _languageFr = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;

    // Libellés traduits
    [ObservableProperty] private string _titleText = "⚙ Settings";
    [ObservableProperty] private string _languageSectionTitle = "Language";
    [ObservableProperty] private string _businessSectionTitle = "Business Software Detection";
    [ObservableProperty] private string _businessDescription = "If this process is running, backup jobs will be blocked. (e.g. Calculator)";
    [ObservableProperty] private string _encryptSectionTitle = "CryptoSoft Encryption";
    [ObservableProperty] private string _encryptDescription = "Extensions to encrypt (space-separated). e.g: .txt .docx .pdf";
    [ObservableProperty] private string _encryptKeyLabel = "Encryption Key";
    [ObservableProperty] private string _logSectionTitle = "Log File Format";
    [ObservableProperty] private string _saveButtonText = "💾 Save Settings";

    // Erreurs de validation
    [ObservableProperty] private string _encryptedExtensionsError = string.Empty;
    [ObservableProperty] private string _encryptionKeyError = string.Empty;
    [ObservableProperty] private string _businessSoftwareError = string.Empty;

    public SettingsViewModel(ISettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _settingsRepo.Load();
        BusinessSoftware = s.BusinessSoftware;
        EncryptionKey = s.EncryptionKey;
        EncryptedExtensions = string.Join(" ", s.EncryptedExtensions);
        LogFormatXml = s.LogFormat == "XML";
        LanguageFr = s.Language == "FR";
        ApplyLanguage(s.Language);
    }

    public void ApplyLanguage(string lang)
    {
        if (lang == "FR")
        {
            TitleText = "⚙ Paramètres";
            LanguageSectionTitle = "Langue";
            BusinessSectionTitle = "Détection logiciel métier";
            BusinessDescription = "Si ce processus est en cours, les sauvegardes seront bloquées. (ex: Calculator)";
            EncryptSectionTitle = "Chiffrement CryptoSoft";
            EncryptDescription = "Extensions à chiffrer (séparées par un espace). ex: .txt .docx .pdf";
            EncryptKeyLabel = "Clé de chiffrement";
            LogSectionTitle = "Format du fichier log";
            SaveButtonText = "💾 Sauvegarder";
        }
        else
        {
            TitleText = "⚙ Settings";
            LanguageSectionTitle = "Language";
            BusinessSectionTitle = "Business Software Detection";
            BusinessDescription = "If this process is running, backup jobs will be blocked. (e.g. Calculator)";
            EncryptSectionTitle = "CryptoSoft Encryption";
            EncryptDescription = "Extensions to encrypt (space-separated). e.g: .txt .docx .pdf";
            EncryptKeyLabel = "Encryption Key";
            LogSectionTitle = "Log File Format";
            SaveButtonText = "💾 Save Settings";
        }
    }

    private bool Validate()
    {
        var valid = true;
        var exts = EncryptedExtensions.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bad = exts.Where(e => !e.StartsWith('.')).ToList();

        if (bad.Count > 0)
        {
            EncryptedExtensionsError = LanguageFr
                ? $"⚠ Extensions invalides: {string.Join(", ", bad)}"
                : $"⚠ Invalid extensions: {string.Join(", ", bad)}";
            valid = false;
        }
        else EncryptedExtensionsError = string.Empty;

        if (exts.Length > 0 && string.IsNullOrWhiteSpace(EncryptionKey))
        {
            EncryptionKeyError = LanguageFr ? "⚠ Clé requise." : "⚠ Encryption key required.";
            valid = false;
        }
        else EncryptionKeyError = string.Empty;

        BusinessSoftwareError = string.Empty;
        return valid;
    }

    [RelayCommand]
    private void Save()  // ← void, pas async → bouton jamais bloqué
    {
        if (!Validate()) return;

        var extensions = EncryptedExtensions
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.StartsWith('.') ? e.ToLower() : $".{e.ToLower()}")
            .ToList();

        var lang = LanguageFr ? "FR" : "EN";

        _settingsRepo.Save(new AppSettings
        {
            BusinessSoftware = BusinessSoftware.Trim(),
            EncryptionKey = EncryptionKey.Trim(),
            EncryptedExtensions = extensions,
            LogFormat = LogFormatXml ? "XML" : "JSON",
            Language = lang
        });

        ApplyLanguage(lang);
        LanguageChanged?.Invoke(lang);

        StatusIsError = false;
        StatusMessage = LanguageFr ? "✔ Paramètres sauvegardés." : "✔ Settings saved.";

        // ── Lance le timer d'effacement en arrière-plan sans bloquer le bouton
        _hideMsgCts?.Cancel();
        _hideMsgCts = new CancellationTokenSource();
        var token = _hideMsgCts.Token;

        Task.Delay(5000, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                StatusMessage = string.Empty;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}