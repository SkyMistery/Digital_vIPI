namespace Vipi.Application.Auth;

/// <summary>
/// Config dell'autorizzazione (sezione <c>Auth</c>). <b>Chi può cosa</b> si decide qui e non in
/// <see cref="DivisionOptions"/>: quella dice <i>qual è</i> la divisione (codice, prefissi ICAO), questa
/// dice <i>a chi</i> quei codici danno un permesso. Carta
/// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c>.
///
/// <para>⚠️ <b>Il binder della configurazione AGGIUNGE alle liste di default invece di sostituirle.</b>
/// Elencare qui tre ruoli non toglie gli altri: li somma. L'unica lista che <i>sostituisce</i> è
/// <see cref="AdminStaffCodes"/>, ed è per questo che esiste.</para>
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// I VID che sono <c>Admin</c> comunque, qualunque posizione staff abbiano (o non abbiano).
    ///
    /// <para><b>È l'antidoto al blocco, non un privilegio di comodo.</b> Senza, un errore nei pattern
    /// produce «nessuno è admin» — e quello stato non si ripara da dentro, perché assegnare permessi
    /// richiede di essere admin. Con un VID qui la porta si riapre sempre.</para>
    /// </summary>
    public List<int> FounderVids { get; set; } = new();

    /// <summary>
    /// Suffissi (pattern regex senza prefisso) dei ruoli di <b>direzione</b> della divisione: il codice
    /// finale è <c>^{Division:Code}-{suffisso}$</c>, cioè <c>IT-DIR</c>, <c>IT-AWM</c>, …
    ///
    /// <para>Otto codici, scelti dal committente il 28 agosto 2026. Chi sta fuori da questo elenco ma ha
    /// un codice <c>IT-…</c> è <c>DivisionStaff</c>, non admin: <c>IT-T01</c>, <c>IT-FOC</c>,
    /// <c>IT-AOA1</c> e gli altri <b>non editano più</b>, e la via per rimetterli in gioco è la promozione
    /// a mano per VID, non questa lista.</para>
    /// </summary>
    public List<string> AdminRoles { get; set; } = new()
    {
        "DIR", "ADIR", "WM", "AWM", "AOC", "AOAC", "SOC", "SOAC",
    };

    /// <summary>
    /// Suffissi dei ruoli <b>ACC-scoped</b> che valgono <c>Editor</c>: il codice finale è
    /// <c>^{prefissoIcao}[A-Z0-9]+-{suffisso}$</c> per ogni prefisso in <c>Division:IcaoPrefixes</c>, cioè
    /// <c>LIRR-CH</c>, <c>LIMM-ACH</c>, …
    /// </summary>
    public List<string> EditorAccRoles { get; set; } = new()
    {
        "CH", "ACH",
    };

    /// <summary>
    /// Pattern regex <b>completi</b> che sostituiscono in blocco quelli dell'admin (<see cref="AdminRoles"/>
    /// più il prefisso di divisione). Vuoto = si usano i default.
    ///
    /// <para>Esiste per <b>restringere</b>: è l'unica lista di questa sezione che sostituisce invece di
    /// sommare. Serve anche a spegnere l'admin da variabile d'ambiente senza ricompilare.</para>
    /// </summary>
    public List<string> AdminStaffCodes { get; set; } = new();
}
