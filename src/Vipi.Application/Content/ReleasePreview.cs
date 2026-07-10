using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Anteprima di una release: identità + il <see cref="RawDocument"/> del payload (tutti i tipi doc-based, post-08).</summary>
public sealed record ReleasePreview(ReleaseTargetType Type, string TargetKey, string AiracCycle, RawDocument? Doc);
