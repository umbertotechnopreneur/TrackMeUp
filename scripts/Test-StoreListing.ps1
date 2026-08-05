[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot '..\store\listing.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param(
        [Parameter(Mandatory)] [bool]$Condition,
        [Parameter(Mandatory)] [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Text {
    param(
        [Parameter(Mandatory)] [string]$Value,
        [Parameter(Mandatory)] [string]$Name,
        [int]$MaximumLength = 2000
    )

    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($Value)) -Message "$Name must not be empty."
    Assert-Condition -Condition ($Value.Length -le $MaximumLength) -Message "$Name is longer than $MaximumLength characters."
}

function Assert-Url {
    param(
        [Parameter(Mandatory)] [string]$Value,
        [Parameter(Mandatory)] [string]$Name
    )

    $uri = $null
    $isValid = [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)
    Assert-Condition -Condition ($isValid -and $uri.Scheme -in @('http', 'https')) -Message "$Name must be an absolute HTTP or HTTPS URL."
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $resolvedPath) '..'))
$raw = Get-Content -LiteralPath $resolvedPath -Raw
$listing = $raw | ConvertFrom-Json

Assert-Condition -Condition ($listing.schemaVersion -eq 1) -Message 'Unsupported Store listing schemaVersion.'
Assert-Text -Value $listing.product.name -Name 'product.name' -MaximumLength 80
Assert-Text -Value $listing.product.publisher -Name 'product.publisher' -MaximumLength 120
Assert-Text -Value $listing.product.category -Name 'product.category' -MaximumLength 120

foreach ($property in @('sourceCodeUrl', 'publisherUrl', 'privacyPolicyUrl', 'supportUrl')) {
    Assert-Url -Value $listing.product.$property -Name "product.$property"
}

$locales = @($listing.locales.PSObject.Properties)
Assert-Condition -Condition ($locales.Count -gt 0) -Message 'At least one Store locale is required.'

foreach ($locale in $locales) {
    $copy = $locale.Value
    Assert-Text -Value $copy.displayName -Name "locales.$($locale.Name).displayName" -MaximumLength 80
    Assert-Text -Value $copy.subtitle -Name "locales.$($locale.Name).subtitle" -MaximumLength 200
    Assert-Text -Value $copy.shortDescription -Name "locales.$($locale.Name).shortDescription" -MaximumLength 1000
    Assert-Text -Value $copy.description -Name "locales.$($locale.Name).description" -MaximumLength 10000

    $features = @($copy.features)
    Assert-Condition -Condition ($features.Count -gt 0) -Message "locales.$($locale.Name).features must contain at least one feature."
    foreach ($feature in $features) {
        Assert-Text -Value $feature -Name "locales.$($locale.Name).feature" -MaximumLength 300
    }
}

$screenshotDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $listing.screenshots.directory.Replace('/', '\')))
$screenshotRoot = $screenshotDirectory.TrimEnd('\') + '\'
Assert-Condition -Condition (Test-Path -LiteralPath $screenshotDirectory -PathType Container) -Message "Screenshot directory not found: $screenshotDirectory"

foreach ($item in @($listing.screenshots.items)) {
    Assert-Text -Value $item.path -Name 'screenshots.items.path' -MaximumLength 260
    Assert-Text -Value $item.locale -Name 'screenshots.items.locale' -MaximumLength 20
    Assert-Text -Value $item.caption -Name 'screenshots.items.caption' -MaximumLength 300
    Assert-Text -Value $item.purpose -Name 'screenshots.items.purpose' -MaximumLength 300

    $relativePath = $item.path.Replace('/', '\')
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $relativePath))
    Assert-Condition -Condition ($candidate.StartsWith($screenshotRoot, [StringComparison]::OrdinalIgnoreCase)) -Message "Screenshot path escapes the screenshot directory: $($item.path)"
    Assert-Condition -Condition (Test-Path -LiteralPath $candidate -PathType Leaf) -Message "Screenshot file not found: $candidate"
}

if (-not [string]::IsNullOrWhiteSpace($listing.publishing.partnerCenterMetadataPath)) {
    $metadataPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $listing.publishing.partnerCenterMetadataPath.Replace('/', '\')))
    $repositoryRootPrefix = $repositoryRoot.TrimEnd('\') + '\'
    Assert-Condition -Condition ($metadataPath.StartsWith($repositoryRootPrefix, [StringComparison]::OrdinalIgnoreCase)) -Message 'Partner Center metadata path must stay inside the repository.'
}

$secretPattern = '(?i)(sk-[A-Za-z0-9]|clientSecret|accessToken|api[_-]?key\s*[:=]\s*["''][^"'']+)
Assert-Condition -Condition ($raw -notmatch $secretPattern) -Message 'Store listing appears to contain a credential or access token.'

Write-Host "Store listing validation passed: $resolvedPath"
Write-Host "Locales: $($locales.Name -join ', ')"
Write-Host "Screenshots declared: $(@($listing.screenshots.items).Count)"
