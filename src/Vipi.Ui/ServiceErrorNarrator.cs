using Microsoft.Extensions.Localization;

namespace Vipi.Ui;

/// <summary>
/// Il testo con cui una pagina mostra il rifiuto di un service — terzo della famiglia, dopo
/// <see cref="AuditNarrator"/> (eventi del registro) e <see cref="ConsistencyNarrator"/> (rilievi di
/// diagnostica), e con lo stesso patto: ⚠️ <b>chiave sconosciuta ⇒ testo grezzo</b>, mai il nome della
/// chiave a video e mai una riga vuota.
///
/// <para><b>Perché non basta <c>ex.Message</c>.</b> Un service Application non ha una lingua
/// d'interfaccia — gira dai job, dai test e dai contesti dove una cultura non c'è — quindi i suoi messaggi
/// sono in italiano, e in pagina inglese si leggevano in italiano. Localizzare alla <i>scrittura</i> non si
/// può; da qui la coppia <c>Message</c> (grezzo, per log e test) + <c>Key</c> (per chi lo mostra), che
/// <see cref="Application.Aor.ValidationException"/> porta dal 22 agosto 2026.</para>
///
/// <para>⚠️ La chiave è facoltativa: i punti che non l'hanno ancora mostrano il messaggio grezzo, com'era
/// prima. Non è un difetto da chiudere in un giro solo — si prende quando si tocca il service.</para>
/// </summary>
public static class ServiceErrorNarrator
{
    /// <summary>La frase da mostrare per un rifiuto di validazione.</summary>
    public static string Testo(Application.Aor.ValidationException ex, IStringLocalizer L) =>
        Tradotto(ex.Key, ex.Args, L) ?? ex.Message;

    private static string? Tradotto(string? chiave, object[]? argomenti, IStringLocalizer L)
    {
        if (string.IsNullOrWhiteSpace(chiave)) return null;
        var s = argomenti is { Length: > 0 } ? L[chiave, argomenti] : L[chiave];
        return s.ResourceNotFound ? null : s.Value;
    }
}
