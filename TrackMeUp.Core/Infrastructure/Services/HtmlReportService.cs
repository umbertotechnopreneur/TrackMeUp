// SPDX-License-Identifier: MIT

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
        var strings = new LocalizationService(_store.LoadSettings().UiLanguage);
        var culture = strings.Culture;
        var result = new ReportAggregationService(_store).Build(
            new ReportQuery(date, date, TimeZoneInfo.Local.Id),
            applicationLimit: int.MaxValue,
            cancellationToken: CancellationToken.None);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException("The local daily report could not be aggregated.");
        }

        var report = result.Value;
        var rows = string.Join(Environment.NewLine, report.Applications.Select(application =>
            $"<tr><td>{Html(application.Application)}</td><td>{Html(FormatDuration(strings, application.ActiveSeconds))}</td></tr>"));
        var aiUsage = RenderAiUsage(report.AiUsage, strings);
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var title = strings.Translate(isDigest ? "HtmlReport.DigestTitle" : "HtmlReport.ReportTitle");
        var html = $"<!doctype html><html lang=\"{Html(strings.Language)}\"><head><meta charset=\"utf-8\"><title>TrackMeUp {Html(title)} {date:yyyy-MM-dd}</title><style>body{{font:16px Segoe UI,sans-serif;background:#f8f3ea;color:#173b3f;max-width:720px;margin:40px auto}}section,table{{background:white;border-radius:16px;padding:20px;margin:16px 0;box-shadow:0 8px 24px #173b3f12}}table{{width:100%;border-collapse:collapse}}td,th{{padding:10px;border-bottom:1px solid #eee;text-align:left}}h1{{letter-spacing:.08em}}h2{{margin-top:0}}</style></head><body><h1>TRACK ME UP</h1><p>{Html(dateTime.ToString("D", culture))}</p><section>{Html(strings.Translate("ActivityCalendar.ActiveTime"))}: <strong>{Html(FormatDuration(strings, report.Totals.ActiveSeconds))}</strong><br>{Html(strings.Translate("ActivityCalendar.IdleTime"))}: <strong>{Html(FormatDuration(strings, report.Totals.IdleSeconds))}</strong><br>{Html(strings.Translate("ActivityCalendar.KeyPresses"))}: <strong>{report.Totals.KeyPresses.ToString("N0", culture)}</strong> · {Html(strings.Translate("ActivityCalendar.MouseClicks"))}: <strong>{report.Totals.MouseClicks.ToString("N0", culture)}</strong></section>{aiUsage}<table><tr><th>{Html(strings.Translate("HtmlReport.Application"))}</th><th>{Html(strings.Translate("ActivityCalendar.ActiveTime"))}</th></tr>{rows}</table></body></html>";
        var name = isDigest ? $"trackmeup-digest-{date:yyyy-MM-dd}.html" : $"trackmeup-{date:yyyy-MM-dd}.html";
        var path = Path.Combine(_utilities.ReportsDirectory, name);
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    private static string RenderAiUsage(AiUsageSummary usage, LocalizationService strings)
    {
        if (usage.RequestCount == 0)
        {
            return $"<section><h2>{Html(strings.Translate("HtmlReport.AiUsage"))}</h2><p>{Html(strings.Translate("HtmlReport.NoAiRequests"))}</p></section>";
        }

        var actualCost = usage.ActualCostUsd.HasValue
            ? strings.Format(
                "HtmlReport.ActualCostDetail",
                FormatCost(strings, usage.ActualCostUsd),
                usage.ActualCostRequestCount)
            : strings.Translate("HtmlReport.ActualCostUnavailable");
        var pricingUpdatedAt = usage.EstimatedCostPricingUpdatedAt?.UtcDateTime.ToString(
            "g",
            strings.Culture)
            ?? strings.Translate("Common.NotAvailable");
        var estimatedCost = usage.EstimatedCostUsd.HasValue
            ? strings.Format(
                "HtmlReport.EstimatedCostDetail",
                FormatCost(strings, usage.EstimatedCostUsd),
                usage.EstimatedCostRequestCount,
                pricingUpdatedAt)
            : strings.Translate("HtmlReport.EstimatedCostUnavailable");
        var providers = string.Join(Environment.NewLine, usage.ByProvider.Select(provider =>
            $"<tr><td>{Html(provider.Label)}</td><td>{provider.RequestCount.ToString("N0", strings.Culture)}</td><td>{provider.TotalTokens.ToString("N0", strings.Culture)}</td><td>{Html(FormatCost(strings, provider.ActualCostUsd))}</td><td>{Html(FormatCost(strings, provider.EstimatedCostUsd))}</td></tr>"));
        var requestSummary = strings.Format(
            "HtmlReport.RequestSummary",
            usage.RequestCount,
            usage.SuccessfulRequestCount,
            usage.FailedRequestCount);
        var tokenSummary = strings.Format(
            "HtmlReport.TokenSummary",
            usage.TotalTokens,
            usage.InputTokens,
            usage.OutputTokens);
        return $"<section><h2>{Html(strings.Translate("HtmlReport.AiUsage"))}</h2>{Html(requestSummary)}<br>{Html(tokenSummary)}<br>{Html(strings.Translate("HtmlReport.ActualCostLabel"))}: <strong>{Html(actualCost)}</strong><br>{Html(strings.Translate("HtmlReport.EstimatedCostLabel"))}: <strong>{Html(estimatedCost)}</strong><table><tr><th>{Html(strings.Translate("HtmlReport.Provider"))}</th><th>{Html(strings.Translate("HtmlReport.Requests"))}</th><th>{Html(strings.Translate("HtmlReport.Tokens"))}</th><th>{Html(strings.Translate("HtmlReport.ActualCostLabel"))}</th><th>{Html(strings.Translate("HtmlReport.EstimatedCostLabel"))}</th></tr>{providers}</table></section>";
    }

    private static string FormatDuration(LocalizationService strings, long seconds)
    {
        var normalized = Math.Max(0, seconds);
        return strings.Format(
            "ActivityCalendar.Duration",
            normalized / 3600,
            (normalized % 3600) / 60,
            normalized % 60);
    }

    private static string FormatCost(LocalizationService strings, decimal? value) =>
        value.HasValue
            ? "$" + value.Value.ToString("0.######", strings.Culture)
            : strings.Translate("Common.NotAvailable");

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
