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
    private TranslationService _t;
    private CancellationTokenSource? _hideMsgCts;

    public event Action<string>? LanguageChanged;
    public event Action? SettingsSaved;

    // ── Valeurs des champs ────────────────────────────────────────────────
    [ObservableProperty] private string _businessSoftware = string.Empty;
    [ObservableProperty] private string _encryptionKey = string.Empty;
    [ObservableProperty] private string _encryptedExtensions = string.Empty;
    [ObservableProperty] private bool _logFormatXml = false;
    [ObservableProperty] private bool _languageFr = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── v3.0 — nouveaux champs ────────────────────────────────────────────
    [ObservableProperty] private string _priorityExtensions = string.Empty; // ex: ".pdf .docx"
    [ObservableProperty] private string _maxParallelFileSize = "1024";       // en KB
    [ObservableProperty] private bool _logToLocal = true;
    [ObservableProperty] private bool _logToDocker = false;
    [ObservableProperty] private string _dockerLogUrl = string.Empty;

    // ── Libellés traduits ─────────────────────────────────────────────────
    [ObservableProperty] private string _titleText = string.Empty;
    [ObservableProperty] private string _languageSectionTitle = string.Empty;
    [ObservableProperty] private string _businessSectionTitle = string.Empty;
    [ObservableProperty] private string _businessDescription = string.Empty;
    [ObservableProperty] private string _encryptSectionTitle = string.Empty;
    [ObservableProperty] private string _encryptDescription = string.Empty;
    [ObservableProperty] private string _encryptKeyLabel = string.Empty;
    [ObservableProperty] private string _logSectionTitle = string.Empty;
    [ObservableProperty] private string _saveButtonText = string.Empty;
    [ObservableProperty] private string _prioritySectionTitle = string.Empty;
    [ObservableProperty] private string _priorityDescription = string.Empty;
    [ObservableProperty] private string _maxSizeLabel = string.Empty;
    [ObservableProperty] private string _logDestSectionTitle = string.Empty;

    // ── Erreurs de validation ─────────────────────────────────────────────
    [ObservableProperty] private string _encryptedExtensionsError = string.Empty;
    [ObservableProperty] private string _encryptionKeyError = string.Empty;
    [ObservableProperty] private string _businessSoftwareError = string.Empty;
    [ObservableProperty] private string _maxSizeError = string.Empty;
    [ObservableProperty] private string _dockerUrlError = string.Empty;

    public SettingsViewModel(ISettingsRepository settingsRepo, TranslationService t)
    {
        _settingsRepo = settingsRepo;
        _t = t;
        LoadSettings();
        UpdateTranslations(_t);
    }

    public void UpdateTranslations(TranslationService t)
    {
        _t = t;
        TitleText = _t.T("settings.title");
        LanguageSectionTitle = _t.T("settings.language");
        BusinessSectionTitle = _t.T("settings.business.title");
        BusinessDescription = _t.T("settings.business.desc");
        EncryptSectionTitle = _t.T("settings.encrypt.title");
        EncryptDescription = _t.T("settings.encrypt.desc");
        EncryptKeyLabel = _t.T("settings.encrypt.keyLabel");
        LogSectionTitle = _t.T("settings.log.title");
        SaveButtonText = _t.T("settings.save");
        PrioritySectionTitle = _t.T("settings.priority.title");
        PriorityDescription = _t.T("settings.priority.desc");
        MaxSizeLabel = _t.T("settings.maxSize.label");
        LogDestSectionTitle = _t.T("settings.logDest.title");
    }

    private void LoadSettings()
    {
        var s = _settingsRepo.Load();
        BusinessSoftware = s.BusinessSoftware;
        EncryptionKey = s.EncryptionKey;
        EncryptedExtensions = string.Join(" ", s.EncryptedExtensions);
        LogFormatXml = s.LogFormat == "XML";
        LanguageFr = s.Language == "FR";
        PriorityExtensions = string.Join(" ", s.PriorityExtensions);
        MaxParallelFileSize = s.MaxParallelFileSize.ToString();
        LogToLocal = s.LogDestination is "Local" or "Both";
        LogToDocker = s.LogDestination is "Docker" or "Both";
        DockerLogUrl = s.DockerLogUrl;
    }

    private bool Validate()
    {
        var valid = true;

        // Extensions chiffrées
        var exts = EncryptedExtensions.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bad = exts.Where(e => !e.StartsWith('.')).ToList();
        if (bad.Count > 0)
        { EncryptedExtensionsError = string.Format(_t.T("err.invalidExtensions"), string.Join(", ", bad)); valid = false; }
        else EncryptedExtensionsError = string.Empty;

        if (exts.Length > 0 && string.IsNullOrWhiteSpace(EncryptionKey))
        { EncryptionKeyError = _t.T("err.keyRequired"); valid = false; }
        else EncryptionKeyError = string.Empty;

        // Taille max
        if (!long.TryParse(MaxParallelFileSize, out var size) || size < 0)
        { MaxSizeError = _t.T("err.maxSizeInvalid"); valid = false; }
        else MaxSizeError = string.Empty;

        // URL Docker
        if (LogToDocker && string.IsNullOrWhiteSpace(DockerLogUrl))
        { DockerUrlError = _t.T("err.dockerUrlRequired"); valid = false; }
        else DockerUrlError = string.Empty;

        BusinessSoftwareError = string.Empty;
        return valid;
    }

    [RelayCommand]
    private void Save()
    {
        if (!Validate()) return;

        var encryptExts = EncryptedExtensions
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.StartsWith('.') ? e.ToLower() : $".{e.ToLower()}")
            .ToList();

        var priorityExts = PriorityExtensions
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.StartsWith('.') ? e.ToLower() : $".{e.ToLower()}")
            .ToList();

        var logDest = (LogToLocal, LogToDocker) switch
        {
            (true, true) => "Both",
            (false, true) => "Docker",
            _ => "Local"
        };

        var lang = LanguageFr ? "FR" : "EN";

        _settingsRepo.Save(new AppSettings
        {
            BusinessSoftware = BusinessSoftware.Trim(),
            EncryptionKey = EncryptionKey.Trim(),
            EncryptedExtensions = encryptExts,
            LogFormat = LogFormatXml ? "XML" : "JSON",
            Language = lang,
            PriorityExtensions = priorityExts,
            MaxParallelFileSize = long.Parse(MaxParallelFileSize),
            LogDestination = logDest,
            DockerLogUrl = DockerLogUrl.Trim()
        });

        LanguageChanged?.Invoke(lang);
        SettingsSaved?.Invoke();

        StatusMessage = _t.T("settings.saved");

        _hideMsgCts?.Cancel();
        _hideMsgCts = new CancellationTokenSource();
        var token = _hideMsgCts.Token;
        Task.Delay(5000, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested) StatusMessage = string.Empty;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}