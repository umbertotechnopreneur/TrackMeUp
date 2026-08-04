using System.Collections.Generic;

namespace TrackMeUp.Services;

/// <summary>
/// Localizes UI labels for supported locales.
/// </summary>
public sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _dictionary = new()
    {
        ["en"] = new()
        {
            ["StateRunning"] = "RUNNING",
            ["StatePaused"] = "PAUSED",
            ["StateIdleContext"] = "Idle",
            ["StateReady"] = "Ready to track",
            ["NoSession"] = "No session recorded",
            ["ApiKeyMissing"] = "Insert your API key first.",
            ["ApiKeySaved"] = "Stored in Windows user environment only. Not persisted by app.",
            ["ContextPlaceholder"] = "Ready to measure your time",
            ["OptionsSaved"] = "Options saved in local appsettings.json.",
            ["ReportCreated"] = "Report created",
            ["MenuTitleOptions"] = "App options",
            ["MenuTitleAbout"] = "About",
            ["MenuToggleOpenAi"] = "OpenAI integration",
            ["MenuToggleScreenshot"] = "Take screenshot",
            ["TrackingNoticeRunning"] = "TrackMeUp running...",
            ["TrackingNoticePaused"] = "TrackMeUp paused",
            ["ScreenshotDisabledHint"] = "Screenshots disabled",
            ["LastSessionTitle"] = "LAST SESSION",
            ["ActiveLabel"] = "ACTIVE",
            ["KeysLabel"] = "KEYS",
            ["ClicksLabel"] = "CLICKS"
        },
        ["it"] = new()
        {
            ["StateRunning"] = "IN CORSO",
            ["StatePaused"] = "IN PAUSA",
            ["StateIdleContext"] = "Inattivo",
            ["StateReady"] = "Pronto a misurare il tuo tempo",
            ["NoSession"] = "Nessuna sessione registrata",
            ["ApiKeyMissing"] = "Inserisci prima una API key.",
            ["ApiKeySaved"] = "Impostata nell'ambiente utente Windows. Non viene salvata dall'app.",
            ["ContextPlaceholder"] = "Pronto a misurare il tuo tempo",
            ["OptionsSaved"] = "Opzioni salvate in appsettings.json locale.",
            ["ReportCreated"] = "Report creato",
            ["MenuTitleOptions"] = "Opzioni app",
            ["MenuTitleAbout"] = "About",
            ["MenuToggleOpenAi"] = "OpenAI integration",
            ["MenuToggleScreenshot"] = "Take screenshot",
            ["TrackingNoticeRunning"] = "TrackMeUp in esecuzione...",
            ["TrackingNoticePaused"] = "TrackMeUp in pausa",
            ["ScreenshotDisabledHint"] = "Screenshot disattivati",
            ["LastSessionTitle"] = "ULTIMA SESSIONE",
            ["ActiveLabel"] = "ATTIVO",
            ["KeysLabel"] = "TASTI",
            ["ClicksLabel"] = "CLICK"
        },
        ["fr"] = new()
        {
            ["StateRunning"] = "EN COURS",
            ["StatePaused"] = "EN PAUSE",
            ["StateIdleContext"] = "Inactif",
            ["StateReady"] = "Prêt à mesurer votre temps",
            ["NoSession"] = "Aucune session enregistrée",
            ["ApiKeyMissing"] = "Saisissez d'abord la clé API.",
            ["ApiKeySaved"] = "Clé enregistrée dans l'environnement utilisateur Windows. Non conservée par l'application.",
            ["ContextPlaceholder"] = "Prêt à mesurer votre temps",
            ["OptionsSaved"] = "Options enregistrées dans appsettings.json local.",
            ["ReportCreated"] = "Rapport créé",
            ["MenuTitleOptions"] = "Options",
            ["MenuTitleAbout"] = "À propos",
            ["MenuToggleOpenAi"] = "Intégration OpenAI",
            ["MenuToggleScreenshot"] = "Capturer l'écran",
            ["TrackingNoticeRunning"] = "TrackMeUp est en cours...",
            ["TrackingNoticePaused"] = "TrackMeUp est en pause",
            ["ScreenshotDisabledHint"] = "Captures désactivées",
            ["LastSessionTitle"] = "DERNIÈRE SESSION",
            ["ActiveLabel"] = "ACTIF",
            ["KeysLabel"] = "TOUCHES",
            ["ClicksLabel"] = "CLICS"
        },
        ["de"] = new()
        {
            ["StateRunning"] = "IN ESECUZIONE",
            ["StatePaused"] = "IN PAUSA",
            ["StateIdleContext"] = "Inaktiv",
            ["StateReady"] = "Bereit, Ihre Zeit zu messen",
            ["NoSession"] = "Keine Sitzung aufgezeichnet",
            ["ApiKeyMissing"] = "Bitte zuerst den API-Schlüssel eingeben.",
            ["ApiKeySaved"] = "In die Windows-Benutzerumgebung gespeichert. Nicht in der App gespeichert.",
            ["ContextPlaceholder"] = "Bereit, Ihre Zeit zu messen",
            ["OptionsSaved"] = "Optionen in lokaler appsettings.json gespeichert.",
            ["ReportCreated"] = "Bericht erstellt",
            ["MenuTitleOptions"] = "App-Einstellungen",
            ["MenuTitleAbout"] = "Über",
            ["MenuToggleOpenAi"] = "OpenAI-Integration",
            ["MenuToggleScreenshot"] = "Screenshot erstellen",
            ["TrackingNoticeRunning"] = "TrackMeUp wird ausgeführt...",
            ["TrackingNoticePaused"] = "TrackMeUp ist pausiert",
            ["ScreenshotDisabledHint"] = "Screenshots deaktiviert",
            ["LastSessionTitle"] = "LETZTE SITZUNG",
            ["ActiveLabel"] = "AKTIV",
            ["KeysLabel"] = "TASTEN",
            ["ClicksLabel"] = "KLICKS"
        },
        ["es"] = new()
        {
            ["StateRunning"] = "EN CURSO",
            ["StatePaused"] = "PAUSADO",
            ["StateIdleContext"] = "Inactivo",
            ["StateReady"] = "Listo para medir tu tiempo",
            ["NoSession"] = "No hay sesión registrada",
            ["ApiKeyMissing"] = "Introduce primero una clave API.",
            ["ApiKeySaved"] = "Guardada en entorno de usuario de Windows. No se guarda en la app.",
            ["ContextPlaceholder"] = "Listo para medir tu tiempo",
            ["OptionsSaved"] = "Opciones guardadas en appsettings.json local.",
            ["ReportCreated"] = "Informe creado",
            ["MenuTitleOptions"] = "Opciones",
            ["MenuTitleAbout"] = "Acerca de",
            ["MenuToggleOpenAi"] = "Integración OpenAI",
            ["MenuToggleScreenshot"] = "Tomar captura",
            ["TrackingNoticeRunning"] = "TrackMeUp se está ejecutando...",
            ["TrackingNoticePaused"] = "TrackMeUp está en pausa",
            ["ScreenshotDisabledHint"] = "Capturas desactivadas",
            ["LastSessionTitle"] = "ÚLTIMA SESIÓN",
            ["ActiveLabel"] = "ACTIVO",
            ["KeysLabel"] = "TECLAS",
            ["ClicksLabel"] = "CLICS"
        },
        ["vi"] = new()
        {
            ["StateRunning"] = "ĐANG THỰC HIỆN",
            ["StatePaused"] = "TẠM DỪNG",
            ["StateIdleContext"] = "Không hoạt động",
            ["StateReady"] = "Sẵn sàng đo thời gian của bạn",
            ["NoSession"] = "Không có phiên làm việc nào",
            ["ApiKeyMissing"] = "Nhập khóa API trước.",
            ["ApiKeySaved"] = "Lưu vào môi trường người dùng Windows. Không được lưu bởi ứng dụng.",
            ["ContextPlaceholder"] = "Sẵn sàng đo thời gian của bạn",
            ["OptionsSaved"] = "Đã lưu tùy chọn vào appsettings.json cục bộ.",
            ["ReportCreated"] = "Đã tạo báo cáo",
            ["MenuTitleOptions"] = "Tùy chọn",
            ["MenuTitleAbout"] = "Giới thiệu",
            ["MenuToggleOpenAi"] = "Tích hợp OpenAI",
            ["MenuToggleScreenshot"] = "Chụp ảnh màn hình",
            ["TrackingNoticeRunning"] = "TrackMeUp đang chạy...",
            ["TrackingNoticePaused"] = "TrackMeUp đã tạm dừng",
            ["ScreenshotDisabledHint"] = "Ảnh chụp đã tắt",
            ["LastSessionTitle"] = "PHIÊN LÀM VIỆC CUỐI CÙNG",
            ["ActiveLabel"] = "HOẠT ĐỘNG",
            ["KeysLabel"] = "PHÍM",
            ["ClicksLabel"] = "BẤM"
        }
    };

    /// <summary>
    /// Selected language code.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Creates service for supported language fallback to English.
    /// </summary>
    public LocalizationService(string language)
    {
        Language = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        if (!_dictionary.ContainsKey(Language))
        {
            Language = "en";
        }
    }

    /// <summary>
    /// Returns translated token text for given key.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <returns>Localized value or fallback key.</returns>
    public string Translate(string key)
        => _dictionary.TryGetValue(Language, out var current) && current.TryGetValue(key, out var value)
            ? value
            : key;
}
