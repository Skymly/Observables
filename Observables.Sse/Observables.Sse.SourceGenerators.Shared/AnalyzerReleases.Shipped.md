; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
OBS8001 | Observables.Sse | Warning | SSE interface member without [SseEvent]
OBS8002 | Observables.Sse | Error | Observables.Sse not referenced
OBS8003 | Observables.Sse | Error | Unsupported return type
OBS8004 | Observables.Sse | Error | [SseEvent] member shape mismatch
OBS8005 | Observables.Sse | Error | Observables.Sse.Reactive required for IObservable
