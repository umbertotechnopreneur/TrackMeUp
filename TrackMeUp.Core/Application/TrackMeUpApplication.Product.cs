// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TrackMeUp.Application;

/// <summary>Provides product metadata and opens allowlisted public product links.</summary>
public sealed partial class TrackMeUpApplication
{
    /// <inheritdoc />
    public Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new ProductInformation(
            "TrackMeUp",
            "MIT License",
            ProductRepositoryUrl,
            ProductAuthorUrl,
            _buildInformation.Load());
        return Task.FromResult(OperationResult<ProductInformation>.Success("product.loaded", "ProductInformationLoaded", info));
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> OpenProductLinkAsync(string linkKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = linkKey switch
        {
            "author" => ProductAuthorUrl,
            "repository" => ProductRepositoryUrl,
            "report-summary" => ProductRepositoryUrl + "/blob/main/docs/AI_REPORT_SUMMARY.md",
            "issues" => ProductIssuesUrl,
            "openweather" => OpenWeatherUrl,
            _ => null
        };
        if (target is null)
        {
            return Task.FromResult(OperationResult<bool>.Failure(
                "product.link.invalid",
                "ProductLinkInvalid",
                new ValidationIssue("linkKey", "unsupported", "ProductLinkInvalid")));
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true })
                ?? throw new InvalidOperationException("Windows did not open the product link.");
            _logger.LogInformation("Product link opened. Link={Link}", linkKey);
            return Task.FromResult(OperationResult<bool>.Success("product.link.opened", "ProductLinkOpened", true));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                NotSupportedException or
                System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning("Product link could not be opened. Link={Link} ExceptionType={ExceptionType}", linkKey, exception.GetType().Name);
            return OperationResult<bool>.Failure("product.link.unavailable", "ProductLinkUnavailable");
        }
    }
}
