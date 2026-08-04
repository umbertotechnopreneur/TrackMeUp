namespace TrackMeUp.Cli;

/// <summary>Provides the compact CLI text catalog without exposing translated text to automation contracts.</summary>
internal static class CliStrings
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ok"] = "OK", ["error"] = "ERROR", ["usage"] = "Usage", ["commands"] = "Commands", ["globalOptions"] = "Global options", ["metric"] = "Metric", ["value"] = "Value", ["state"] = "State", ["context"] = "Context", ["keys"] = "Keys", ["clicks"] = "Clicks", ["activeSeconds"] = "Active seconds", ["intensity"] = "Intensity", ["statusTitle"] = "TrackMeUp status", ["memory"] = "Memory", ["network"] = "Network", ["systemSnapshot"] = "System snapshot", ["settings"] = "Settings", ["setting"] = "Setting", ["type"] = "Type", ["allowed"] = "Allowed values", ["restart"] = "Restart", ["yes"] = "yes", ["no"] = "no", ["notAvailable"] = "n/a"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalog =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English,
            ["it"] = new Dictionary<string, string>(English) { ["ok"] = "OK", ["error"] = "ERRORE", ["usage"] = "Uso", ["commands"] = "Comandi", ["globalOptions"] = "Opzioni globali", ["metric"] = "Metrica", ["value"] = "Valore", ["state"] = "Stato", ["context"] = "Contesto", ["keys"] = "Tasti", ["clicks"] = "Click", ["activeSeconds"] = "Secondi attivi", ["intensity"] = "Intensità", ["statusTitle"] = "Stato TrackMeUp", ["memory"] = "Memoria", ["network"] = "Rete", ["systemSnapshot"] = "Snapshot del sistema", ["notAvailable"] = "n/d" },
            ["vi"] = new Dictionary<string, string>(English) { ["error"] = "LỖI", ["usage"] = "Cách dùng", ["commands"] = "Lệnh", ["globalOptions"] = "Tùy chọn chung", ["metric"] = "Chỉ số", ["value"] = "Giá trị", ["state"] = "Trạng thái", ["context"] = "Ngữ cảnh", ["keys"] = "Phím", ["clicks"] = "Lần nhấp", ["activeSeconds"] = "Giây hoạt động", ["intensity"] = "Cường độ", ["statusTitle"] = "Trạng thái TrackMeUp", ["memory"] = "Bộ nhớ", ["network"] = "Mạng", ["systemSnapshot"] = "Ảnh chụp hệ thống", ["notAvailable"] = "không có" },
            ["fr"] = new Dictionary<string, string>(English) { ["error"] = "ERREUR", ["usage"] = "Utilisation", ["commands"] = "Commandes", ["globalOptions"] = "Options globales", ["metric"] = "Mesure", ["value"] = "Valeur", ["state"] = "État", ["context"] = "Contexte", ["keys"] = "Touches", ["clicks"] = "Clics", ["activeSeconds"] = "Secondes actives", ["intensity"] = "Intensité", ["statusTitle"] = "État TrackMeUp", ["memory"] = "Mémoire", ["network"] = "Réseau", ["systemSnapshot"] = "Instantané système", ["notAvailable"] = "n/d" },
            ["de"] = new Dictionary<string, string>(English) { ["error"] = "FEHLER", ["usage"] = "Verwendung", ["commands"] = "Befehle", ["globalOptions"] = "Globale Optionen", ["metric"] = "Metrik", ["value"] = "Wert", ["state"] = "Status", ["context"] = "Kontext", ["keys"] = "Tasten", ["clicks"] = "Klicks", ["activeSeconds"] = "Aktive Sekunden", ["intensity"] = "Intensität", ["statusTitle"] = "TrackMeUp-Status", ["memory"] = "Arbeitsspeicher", ["network"] = "Netzwerk", ["systemSnapshot"] = "System-Snapshot", ["notAvailable"] = "k. A." },
            ["es"] = new Dictionary<string, string>(English) { ["error"] = "ERROR", ["usage"] = "Uso", ["commands"] = "Comandos", ["globalOptions"] = "Opciones globales", ["metric"] = "Métrica", ["value"] = "Valor", ["state"] = "Estado", ["context"] = "Contexto", ["keys"] = "Teclas", ["clicks"] = "Clics", ["activeSeconds"] = "Segundos activos", ["intensity"] = "Intensidad", ["statusTitle"] = "Estado de TrackMeUp", ["memory"] = "Memoria", ["network"] = "Red", ["systemSnapshot"] = "Instantánea del sistema", ["notAvailable"] = "n/d" }
        };

    /// <summary>Returns the translated CLI string, falling back to its stable key when no catalog entry exists.</summary>
    internal static string Get(string language, string key) =>
        Catalog.TryGetValue(language, out var languageCatalog) && languageCatalog.TryGetValue(key, out var translated)
            ? translated
            : English.TryGetValue(key, out var english) ? english : key;
}
