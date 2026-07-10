using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Identità di una release per la risoluzione della route del viewer (redirect da /vsop/release/{id}).</summary>
public sealed record ReleaseLocation(ReleaseTargetType Type, string TargetKey, string AiracCycle, string AccCode);
