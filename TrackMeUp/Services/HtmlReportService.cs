using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace TrackMeUp.Services;

/// <summary>
/// Builds a local HTML report for today's tracked activity.
/// </summary>
public sealed class HtmlReportService
{
    private readonly LocalStore _store;
    private readonly UtilityService _utilities;

    /// <summary>
    /// Creates report service with dependencies.
    /// </summary>
    public HtmlReportService(LocalStore store, UtilityService utilities) { _store = store; _utilities = utilities; }

    /// <summary>
    /// Exports today's summary and app rows as an encoded HTML file.
    /// </summary>
    /// <returns>Absolute path of the exported report.</returns>
    public string ExportToday()
    {
        return ExportForDate(DateOnly.FromDateTime(DateTime.Today), isDigest: false);
    }

    /// <summary>
    /// Exports a dated daily digest without changing the report presentation contract.
    /// </summary>
    /// <param name="date">Local calendar date to aggregate.</param>
    /// <returns>Absolute path of the exported digest.</returns>
    public string ExportDailyDigest(DateOnly date) => ExportForDate(date, isDigest: true);

    private string ExportForDate(DateOnly date, bool isDigest)
    {
        var summary = _store.GetSummary(date);
        // Only basic app-level totals are included in MVP; richer sections are added in the report roadmap.
        var rows = string.Join(Environment.NewLine, summary.Applications.Select(x => $"<tr><td>{WebUtility.HtmlEncode(x.Application)}</td><td>{WebUtility.HtmlEncode(_utilities.FormatDuration(x.ActiveSeconds))}</td></tr>"));
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var title = isDigest ? "Daily digest" : "Daily report";
        var html = $"<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\"><title>TrackMeUp {title} {date:yyyy-MM-dd}</title><style>body{{font:16px Segoe UI,sans-serif;background:#f8f3ea;color:#173b3f;max-width:720px;margin:40px auto}}section,table{{background:white;border-radius:16px;padding:20px;margin:16px 0;box-shadow:0 8px 24px #173b3f12}}table{{width:100%;border-collapse:collapse}}td{{padding:10px;border-bottom:1px solid #eee}}h1{{letter-spacing:.08em}}</style></head><body><h1>TRACK ME UP</h1><p>{dateTime:dddd d MMMM yyyy}</p><section>Tempo attivo: <strong>{_utilities.FormatDuration(summary.ActiveSeconds)}</strong><br>Tempo inattivo: <strong>{_utilities.FormatDuration(summary.IdleSeconds)}</strong><br>Tasti: <strong>{summary.KeyPresses:N0}</strong> · Click: <strong>{summary.MouseClicks:N0}</strong></section><table><tr><th>Applicazione</th><th>Tempo attivo</th></tr>{rows}</table></body></html>";
        var name = isDigest ? $"trackmeup-digest-{date:yyyy-MM-dd}.html" : $"trackmeup-{date:yyyy-MM-dd}.html";
        var path = Path.Combine(_utilities.ReportsDirectory, name);
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }
}
