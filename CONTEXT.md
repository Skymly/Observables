# Observables

Roslyn source generators that bridge event and IO boundaries onto reactive surfaces (R3 and System.Reactive).

## Language

**Feature (domain)**:
A disk folder `Observables.<Feature>/` that ships as the pair of NuGet packages `Observables.<Feature>.R3` and `Observables.<Feature>.Reactive`.
_Avoid_: module (in the product sense), package family

**Proxy domain catalog**:
The single table of interface-proxy Feature metadata (markers, member diagnostic IDs, default boundary attributes) shared by analyzers and code fixes.
_Avoid_: domain registry, analyzer catalog (when meaning this shared table)

**IO stub generator pipeline**:
The shared incremental wiring for `ForAttributeWithMetadataName` interface-proxy generators (parse → diagnostics → emit), configured per Feature by adapters.
_Avoid_: generator host, stub framework

**Proxy registration emitter**:
The shared emitter of ModuleInitializer factory registration for single-client `*Service.RegisterGeneratedFactory` Features.
_Avoid_: module initializer helper (when meaning this registration template)

**Feature protocol bridge**:
The Feature-local subscribe / unsubscribe / cancel / dispose / payload-map module that both R3 and System.Reactive adapters sit on.
_Avoid_: shared pump, subscribe pump (when meaning this Feature-local module)

**Proxy class emitter**:
The shared emitter of the sealed generated proxy class shell for single-client IO Features.
_Avoid_: class template, proxy shell helper (when meaning this shared emitter)
