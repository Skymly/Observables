# Generates PublicAPI.Shipped.txt baselines for domain runtime projects (M7).
# Usage: ./eng/scripts/bootstrap-public-api.ps1 [-ProjectRelativePaths <csproj>]

param(
    [string[]]$ProjectRelativePaths = @(
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
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$header = '#nullable enable'
$tfms = @('netstandard2.0', 'net8.0', 'net9.0')

Push-Location $repoRoot

function Initialize-TfmPublicApiFiles {
    param([string]$ProjectDir)

    foreach ($tfm in $script:tfms) {
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
    param([string]$ProjectDir)

    foreach ($tfm in $script:tfms) {
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
        Write-Host "==> $relativePath"
        Initialize-TfmPublicApiFiles -ProjectDir $projectDir

        # dotnet format walks all TFMs; per-TFM AdditionalFiles route fixes into PublicAPI/<tfm>/.
        dotnet format analyzers $projectPath --diagnostics RS0016 --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet format analyzers failed for $relativePath"
        }

        Finalize-TfmPublicApiBaseline -ProjectDir $projectDir
    }
}
finally {
    Pop-Location
}

Write-Host 'Public API baselines generated.'
