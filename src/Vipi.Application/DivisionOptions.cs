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
    /// </summary>
    public List<string> AdminRolePatterns { get; set; } = new()
    {
        "DIR", "ADIR", "WM", "AWM", "AOC", "AOAC", @"AOA\d+",
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
