using Microsoft.Extensions.Localization;
using Vipi.Application.Diagnostics;

namespace Vipi.Ui;

/// <summary>
/// Traduce un rilievo di diagnostica nella lingua dell'interfaccia — gemello di <see cref="AuditNarrator"/>,
/// e con lo stesso patto: <b>chiave sconosciuta ⇒ si mostra il testo grezzo, mai una riga vuota</b>.
///
/// <para><b>Perché la traduzione sta qui e non nel rilievo.</b> Fino al 22 agosto 2026 categoria e dettaglio
/// erano stringhe italiane cablate nei produttori: in pagina inglese le intestazioni erano tradotte e il
/// contenuto no — «SEVERE | Gerarchia dangling | ParentCallsign «…» non esiste nei cataloghi». Localizzarle
/// alla <i>scrittura</i> non si poteva: un rilievo nasce anche fuori da una richiesta HTTP (le manutenzioni
/// d'avvio girano prima che l'app accetti richieste) e viene letto anche dove una cultura non c'è —
/// l'health check e i log. Quindi il rilievo porta <b>entrambi</b>: il testo grezzo per loro, la chiave per
/// chi lo mostra.</para>
/// </summary>
public static class ConsistencyNarrator
{
    /// <summary>La famiglia del rilievo, tradotta se il produttore ha dichiarato una chiave.</summary>
    public static string Categoria(ConsistencyFinding f, IStringLocalizer L) =>
        Tradotto(f.CategoryKey, null, L) ?? f.Category;

    /// <summary>La spiegazione, tradotta coi suoi argomenti.</summary>
    public static string Dettaglio(ConsistencyFinding f, IStringLocalizer L) =>
        Tradotto(f.DetailKey, f.DetailArgs, L) ?? f.Detail;

    /// <summary>Il nome dell'area, per i chip e per la Guida.</summary>
    public static string Area(ConsistencyArea area, IStringLocalizer L) => L["Diag_Area_" + area].Value;

    /// <summary>
    /// ⚠️ <c>ResourceNotFound</c> è il solo modo di distinguere «tradotto» da «la chiave non c'è»: il
    /// localizzatore, quando non trova, restituisce <b>il nome della chiave</b> come valore. Senza questo
    /// controllo, una chiave sbagliata comparirebbe a video come <c>Diag_Msg_Qualcosa</c> — che è peggio
    /// dell'italiano in pagina inglese, perché non lo si legge affatto.
    /// </summary>
    private static string? Tradotto(string? chiave, object[]? argomenti, IStringLocalizer L)
    {
        if (string.IsNullOrWhiteSpace(chiave)) return null;
        var s = argomenti is { Length: > 0 } ? L[chiave, argomenti] : L[chiave];
        return s.ResourceNotFound ? null : s.Value;
    }
}
