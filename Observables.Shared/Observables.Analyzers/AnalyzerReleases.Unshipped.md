; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; OBS0001 (behavior change): detection switched from assembly/namespace heuristics to the
; package-declared build_property.Observables<Feature>Backend_<Backend> keys plus a symbol
; fallback; backend model extended to three slots (R3 / Reactive / Primitives per ADR-005);
; message format now takes two args — {0} feature, {1} comma-separated conflicting package ids.
; No rule id, category, or severity change.
; The symbol fallback now ignores generator markers from referenced assemblies.
; OBS0002 exempts supported generic RestAPI interfaces.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
OBS0003 | Observables | Error | Generic IO proxy method is not supported
