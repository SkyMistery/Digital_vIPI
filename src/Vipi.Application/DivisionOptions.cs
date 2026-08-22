namespace Vipi.Application;

/// <summary>
/// Identità della divisione IVAO servita dall'istanza (sezione "Division"). Centralizza tutto ciò che
/// cambia passando divisione (es. IT → DE): codice divisione (prefisso staff code + id API) e prefissi
/// ICAO dei callsign ATC. Il contenuto documentale (seed) resta separato.
/// </summary>
public sealed class DivisionOptions
{
    public const string SectionName = "Division";

    /// <summary>Codice divisione IVAO: prefisso degli staff code (es. "IT" → IT-DIR) e id nell'API membri.</summary>
    public string Code { get; set; } = "IT";

    /// <summary>Nome leggibile (display).</summary>
    public string Name { get; set; } = "Italia";

    /// <summary>Prefissi ICAO dei callsign ATC della divisione (es. IT → ["LI"], DE → ["ED","ET"]).</summary>
    public List<string> IcaoPrefixes { get; set; } = new() { "LI" };

    /// <summary>
    /// Suffissi (pattern regex senza prefisso divisione) considerati admin completo. L'admin code finale è
    /// <c>^{Code}-{suffisso}$</c>. Sovrascrivibili da <c>Auth:AdminStaffCodes</c> (pattern completi).
    ///
    /// <para><b>Il default è un jolly, e non per pigrizia: lo staff di divisione è admin, tutto.</b> Decisione
    /// del committente (22 agosto 2026), presa sui codici veri visti ai login: l'elenco puntuale
    /// (<c>DIR</c>, <c>ADIR</c>, <c>WM</c>, <c>AWM</c>, <c>AOC</c>, <c>AOAC</c>, <c>AOA\d+</c>) lasciava fuori
    /// quattro staffisti reali — <c>IT-SOC</c>, <c>IT-T01</c>, <c>IT-FOC</c>, <c>IT-FOAC</c> — e ogni ruolo
    /// nuovo della divisione sarebbe entrato escluso, senza che nessuno se ne accorgesse fino alla
    /// segnalazione. Un codice <c>{Code}-{qualcosa}</c> lo assegna il portale IVAO <b>solo</b> allo staff
    /// della divisione: il jolly non allarga oltre quell'insieme.</para>
    ///
    /// <para>⚠️ <b>Da qui si può solo ALLARGARE, mai restringere.</b> Il binder della configurazione
    /// <i>aggiunge</i> alle liste di default invece di sostituirle: elencare qui tre ruoli non toglie gli
    /// altri, li somma — e ora che il default è un jolly, aggiungerne altri non cambia nulla. Per restringere
    /// davvero l'insieme degli admin si usa <c>Auth:AdminStaffCodes</c>, che invece sostituisce tutto. È una
    /// differenza che conta: è il permesso più alto del prodotto.</para>
    /// </summary>
    public List<string> AdminRolePatterns { get; set; } = new()
    {
        "[A-Z0-9]+",
    };

    /// <summary>
    /// Suffissi (pattern regex) di ruoli admin <b>ACC-scoped</b>: il codice staff ha il prefisso ICAO dell'ACC, non
    /// quello di divisione (es. <c>LIRR-CH</c>, <c>LIMM-ACH</c>). Il codice admin finale è
    /// <c>^{prefissoIcao}[A-Z0-9]+-{suffisso}$</c> per ogni prefisso in <see cref="IcaoPrefixes"/>.
    /// </summary>
    public List<string> AdminAccRolePatterns { get; set; } = new()
    {
        "CH", "ACH",
    };
}
