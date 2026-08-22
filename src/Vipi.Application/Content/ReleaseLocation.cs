using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Identità di una release per la risoluzione della route del viewer (redirect da /services/vsop/release/{id}).</summary>
public sealed record ReleaseLocation(ReleaseTargetType Type, string TargetKey, string AiracCycle, string AccCode);
