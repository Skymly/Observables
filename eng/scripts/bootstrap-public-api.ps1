# Generates PublicAPI.Shipped.txt baselines for domain runtime + bridge projects.
# Usage: ./eng/scripts/bootstrap-public-api.ps1 [-ProjectRelativePaths <csproj>]
#
# Default project list and TFMs are derived from on-disk PublicAPI/ trees, not a frozen M7 snapshot.

param(
    [string[]]$ProjectRelativePaths = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$header = '#nullable enable'

function Get-PublicApiProjects {
    Get-ChildItem -Path $repoRoot -Recurse -Directory -Filter PublicAPI |
        ForEach-Object {
            $csproj = Get-ChildItem -Path $_.Parent.FullName -Filter '*.csproj' -File | Select-Object -First 1
            if ($csproj) {
                [System.IO.Path]::GetRelativePath($repoRoot, $csproj.FullName).Replace('\', '/')
            }
        } |
        Sort-Object -Unique
}

function Get-PublicApiTfms {
    param([string]$ProjectDir)
    Get-ChildItem -Path (Join-Path $ProjectDir 'PublicAPI') -Directory |
        ForEach-Object Name |
        Sort-Object
}

if ($ProjectRelativePaths.Count -eq 0) {
    $ProjectRelativePaths = @(Get-PublicApiProjects)
}

Push-Location $repoRoot

function Initialize-TfmPublicApiFiles {
    param(
        [string]$ProjectDir,
        [string[]]$Tfms
    )

    foreach ($tfm in $Tfms) {
        $tfmDir = Join-Path $ProjectDir "PublicAPI/$tfm"
        New-Item -ItemType Directory -Force -Path $tfmDir | Out-Null
        Set-Content -Path (Join-Path $tfmDir 'PublicAPI.Shipped.txt') -Value $header -Encoding utf8NoBOM
        Set-Content -Path (Join-Path $tfmDir 'PublicAPI.Unshipped.txt') -Value $header -Encoding utf8NoBOM
    }

    foreach ($name in @('PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt')) {
        $path = Join-Path $ProjectDir $name
        if (Test-Path $path) {
            Remove-Item $path -Force
        }
    }
}

function Finalize-TfmPublicApiBaseline {
    param(
        [string]$ProjectDir,
        [string[]]$Tfms
    )

    foreach ($tfm in $Tfms) {
        $tfmDir = Join-Path $ProjectDir "PublicAPI/$tfm"
        $unshippedPath = Join-Path $tfmDir 'PublicAPI.Unshipped.txt'
        $shippedPath = Join-Path $tfmDir 'PublicAPI.Shipped.txt'
        $lines = @(Get-Content $unshippedPath | Where-Object { $_ -and $_.Trim().Length -gt 0 -and $_ -ne $header })
        $shippedBody = @($header) + ($lines | Sort-Object -Unique)
        Set-Content -Path $shippedPath -Value $shippedBody -Encoding utf8NoBOM
        Set-Content -Path $unshippedPath -Value $header -Encoding utf8NoBOM
    }
}

try {
    foreach ($relativePath in $ProjectRelativePaths) {
        $projectPath = Join-Path $repoRoot $relativePath
        if (-not (Test-Path $projectPath)) {
            throw "Project not found: $projectPath"
        }

        $projectDir = Split-Path $projectPath -Parent
        $tfms = @(Get-PublicApiTfms -ProjectDir $projectDir)
        if ($tfms.Count -eq 0) {
            throw "No PublicAPI/<tfm> directories for $relativePath"
        }

        Write-Host "==> $relativePath ($($tfms -join ', '))"
        Initialize-TfmPublicApiFiles -ProjectDir $projectDir -Tfms $tfms

        # dotnet format walks all TFMs; per-TFM AdditionalFiles route fixes into PublicAPI/<tfm>/.
        dotnet format analyzers $projectPath --diagnostics RS0016 --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet format analyzers failed for $relativePath"
        }

        Finalize-TfmPublicApiBaseline -ProjectDir $projectDir -Tfms $tfms
    }
}
finally {
    Pop-Location
}

Write-Host 'Public API baselines generated.'
