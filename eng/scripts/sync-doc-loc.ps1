# Builds library projects, then checks localized XML doc member parity against generated English XML.
# Usage:
#   ./eng/scripts/sync-doc-loc.ps1 [-ProjectRelativePath <csproj>] [-Culture zh-Hans] [-Configuration Release] [-TargetFramework net8.0]
#   ./eng/scripts/sync-doc-loc.ps1 -AllDomains
#
# Localized XML is maintained under <project>/loc/<culture>/<AssemblyName>.xml.
# Update localized text manually or via your editor workflow; this script does not call external translation services.

param(
    [string]$ProjectRelativePath = 'Observables.Nats/Observables.Nats/Observables.Nats.csproj',
    [string[]]$ProjectRelativePaths = @(),
    [switch]$AllDomains,
    [string]$Culture = 'zh-Hans',
    [string]$Configuration = 'Release',
    [string]$TargetFramework = 'net8.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

$defaultDomainProjects = @(
    'Observables.Nats/Observables.Nats/Observables.Nats.csproj',
    'Observables.Nats/Observables.Nats.Reactive/Observables.Nats.Reactive.csproj',
    'Observables.Sse/Observables.Sse/Observables.Sse.csproj',
    'Observables.Sse/Observables.Sse.Reactive/Observables.Sse.Reactive.csproj',
    'Observables.Grpc/Observables.Grpc/Observables.Grpc.csproj',
    'Observables.Grpc/Observables.Grpc.Reactive/Observables.Grpc.Reactive.csproj',
    'Observables.WebSocket/Observables.WebSocket/Observables.WebSocket.csproj',
    'Observables.WebSocket/Observables.WebSocket.Reactive/Observables.WebSocket.Reactive.csproj',
    'Observables.Mqtt/Observables.Mqtt/Observables.Mqtt.csproj',
    'Observables.Mqtt/Observables.Mqtt.Reactive/Observables.Mqtt.Reactive.csproj',
    'Observables.SignalR/Observables.SignalR/Observables.SignalR.csproj',
    'Observables.SignalR/Observables.SignalR.Reactive/Observables.SignalR.Reactive.csproj',
    'Observables.RestAPI/Observables.RestAPI/Observables.RestAPI.csproj',
    'Observables.RestAPI/Observables.RestAPI.Reactive/Observables.RestAPI.Reactive.csproj'
)

function Get-DocMemberNames {
    param([string]$XmlPath)

    if (-not (Test-Path $XmlPath)) {
        throw "XML not found: $XmlPath"
    }

    [xml]$doc = Get-Content -Path $XmlPath -Encoding UTF8
    $names = @(
        $doc.doc.members.member |
            ForEach-Object { $_.name } |
            Where-Object { $_ } |
            Sort-Object -Unique
    )
    return ,$names
}

function Test-DocLocalizationParity {
    param([string]$RelativePath)

    $projectPath = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path $projectPath)) {
        throw "Project not found: $projectPath"
    }

    $projectDir = Split-Path $projectPath -Parent
    Write-Host "==> Building $RelativePath ($Configuration, $TargetFramework)"
    dotnet build $projectPath -c $Configuration -f $TargetFramework --no-restore 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        dotnet build $projectPath -c $Configuration -f $TargetFramework
        throw "dotnet build failed for $RelativePath"
    }

    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $projectXml = [xml](Get-Content $projectPath -Encoding UTF8)
    $assemblyNameNode = $projectXml.Project.PropertyGroup.AssemblyName
    if ($assemblyNameNode) {
        $assemblyName = [string]$assemblyNameNode
    }

    $englishXmlPath = Join-Path $projectDir "bin/$Configuration/$TargetFramework/$assemblyName.xml"
    $localizedXmlPath = Join-Path $projectDir "loc/$Culture/$assemblyName.xml"

    Write-Host "    English:    $englishXmlPath"
    Write-Host "    Localized:  $localizedXmlPath"

    if (-not (Test-Path $localizedXmlPath)) {
        throw "Localized XML not found: $localizedXmlPath"
    }

    $englishMembers = Get-DocMemberNames -XmlPath $englishXmlPath
    $localizedMembers = Get-DocMemberNames -XmlPath $localizedXmlPath

    $missingInLocalized = @($englishMembers | Where-Object { $_ -notin $localizedMembers })
    $extraInLocalized = @($localizedMembers | Where-Object { $_ -notin $englishMembers })

    $hasIssues = $false

    if ($missingInLocalized.Count -gt 0) {
        $hasIssues = $true
        Write-Host ''
        Write-Host "Missing in loc/$Culture (present in generated English XML):" -ForegroundColor Yellow
        $missingInLocalized | ForEach-Object { Write-Host "  $_" }
    }

    if ($extraInLocalized.Count -gt 0) {
        $hasIssues = $true
        Write-Host ''
        Write-Host "Extra in loc/$Culture (not in generated English XML):" -ForegroundColor Yellow
        $extraInLocalized | ForEach-Object { Write-Host "  $_" }
    }

    if ($hasIssues) {
        Write-Host ''
        Write-Host "Doc localization parity check failed for $RelativePath." -ForegroundColor Red
        return $false
    }

    Write-Host ''
    Write-Host "Doc localization parity OK ($($englishMembers.Count) members, culture=$Culture)."
    return $true
}

$projectsToCheck = @()
if ($AllDomains) {
    $projectsToCheck = $defaultDomainProjects
}
elseif ($ProjectRelativePaths.Count -gt 0) {
    $projectsToCheck = $ProjectRelativePaths
}
else {
    $projectsToCheck = @($ProjectRelativePath)
}

Push-Location $repoRoot
try {
    $failedProjects = @()
    foreach ($relativePath in $projectsToCheck) {
        if (-not (Test-DocLocalizationParity -RelativePath $relativePath)) {
            $failedProjects += $relativePath
        }
    }

    if ($failedProjects.Count -gt 0) {
        Write-Host ''
        Write-Host "Doc localization parity check failed for $($failedProjects.Count) project(s)." -ForegroundColor Red
        $failedProjects | ForEach-Object { Write-Host "  $_" }
        exit 1
    }

    if ($projectsToCheck.Count -gt 1) {
        Write-Host ''
        Write-Host "All $($projectsToCheck.Count) projects passed doc localization parity."
    }
}
finally {
    Pop-Location
}
