using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// In che lingua è scritto un documento, e se si legge in quella sola (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c>).
///
/// <para>⚠️ Le due cose viaggiano insieme perché si decidono insieme: «bloccato» senza sapere <b>in quale
/// lingua</b> non vuol dire niente, e il pannello che le mostra le mostra sulla stessa riga.</para>
/// </summary>
/// <param name="Language">La lingua in cui il documento si redige.</param>
/// <param name="Locked">Vero se si serve sempre in quella lingua, senza traduzione.</param>
public sealed record DocumentLanguageState(Language Language, bool Locked);
