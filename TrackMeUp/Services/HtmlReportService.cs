using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using TrackMeUp.Application;

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
        var result = new ReportAggregationService(_store).Build(
            new ReportQuery(date, date, TimeZoneInfo.Local.Id),
            applicationLimit: int.MaxValue,
            cancellationToken: CancellationToken.None);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException("The local daily report could not be aggregated.");
        }

        var report = result.Value;
        var rows = string.Join(Environment.NewLine, report.Applications.Select(x => $"<tr><td>{WebUtility.HtmlEncode(x.Application)}</td><td>{WebUtility.HtmlEncode(_utilities.FormatDuration(x.ActiveSeconds))}</td></tr>"));
        var aiUsage = RenderAiUsage(report.AiUsage);
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var title = isDigest ? "Daily digest" : "Daily report";
        var html = $"<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\"><title>TrackMeUp {title} {date:yyyy-MM-dd}</title><style>body{{font:16px Segoe UI,sans-serif;background:#f8f3ea;color:#173b3f;max-width:720px;margin:40px auto}}section,table{{background:white;border-radius:16px;padding:20px;margin:16px 0;box-shadow:0 8px 24px #173b3f12}}table{{width:100%;border-collapse:collapse}}td,th{{padding:10px;border-bottom:1px solid #eee;text-align:left}}h1{{letter-spacing:.08em}}h2{{margin-top:0}}</style></head><body><h1>TRACK ME UP</h1><p>{dateTime:dddd d MMMM yyyy}</p><section>Tempo attivo: <strong>{_utilities.FormatDuration(report.Totals.ActiveSeconds)}</strong><br>Tempo inattivo: <strong>{_utilities.FormatDuration(report.Totals.IdleSeconds)}</strong><br>Tasti: <strong>{report.Totals.KeyPresses:N0}</strong> · Click: <strong>{report.Totals.MouseClicks:N0}</strong></section>{aiUsage}<table><tr><th>Applicazione</th><th>Tempo attivo</th></tr>{rows}</table></body></html>";
        var name = isDigest ? $"trackmeup-digest-{date:yyyy-MM-dd}.html" : $"trackmeup-{date:yyyy-MM-dd}.html";
        var path = Path.Combine(_utilities.ReportsDirectory, name);
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    private static string RenderAiUsage(AiUsageSummary usage)
    {
        if (usage.RequestCount == 0)
        {
            return "<section><h2>Utilizzo AI</h2><p>Nessuna richiesta AI nel periodo.</p></section>";
        }

        var actualCost = usage.ActualCostUsd.HasValue
            ? $"${usage.ActualCostUsd.Value:0.######} ({usage.ActualCostRequestCount} richieste con costo restituito dal provider)"
            : "non restituito dal provider";
        var pricingUpdatedAt = usage.EstimatedCostPricingUpdatedAt?.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture)
            ?? "n/d";
        var estimatedCost = usage.EstimatedCostUsd.HasValue
            ? $"${usage.EstimatedCostUsd.Value:0.######} ({usage.EstimatedCostRequestCount} richieste stimate, prezzi aggiornati {pricingUpdatedAt} UTC)"
            : "non stimabile con il listino locale";
        var providers = string.Join(Environment.NewLine, usage.ByProvider.Select(provider =>
            $"<tr><td>{WebUtility.HtmlEncode(provider.Label)}</td><td>{provider.RequestCount:N0}</td><td>{provider.TotalTokens:N0}</td><td>{FormatCost(provider.ActualCostUsd)}</td><td>{FormatCost(provider.EstimatedCostUsd)}</td></tr>"));
        return $"<section><h2>Utilizzo AI</h2>Richieste: <strong>{usage.RequestCount:N0}</strong> ({usage.SuccessfulRequestCount:N0} riuscite, {usage.FailedRequestCount:N0} non riuscite)<br>Token: <strong>{usage.TotalTokens:N0}</strong> (input {usage.InputTokens:N0}, output {usage.OutputTokens:N0})<br>Costo effettivo: <strong>{actualCost}</strong><br>Costo stimato: <strong>{estimatedCost}</strong><table><tr><th>Provider</th><th>Richieste</th><th>Token</th><th>Costo effettivo</th><th>Costo stimato</th></tr>{providers}</table></section>";
    }

    private static string FormatCost(decimal? value) =>
        value.HasValue ? "$" + value.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) : "n/d";
}
