namespace Vipi.Application;

/// <summary>
/// Identità della divisione IVAO servita dall'istanza (sezione "Division"). Centralizza tutto ciò che
/// cambia passando divisione (es. IT → DE): codice divisione (prefisso staff code + id API) e prefissi
/// ICAO dei callsign ATC. Il contenuto documentale (seed) resta separato.
///
/// <para>⚠️ <b>Qui non sta più chi può cosa.</b> Le due liste che c'erano — <c>AdminRolePatterns</c> (il
/// jolly <c>[A-Z0-9]+</c>) e <c>AdminAccRolePatterns</c> (<c>CH</c>, <c>ACH</c>) — sono morte il 28 agosto
/// 2026 con le autorizzazioni a livelli, e sono passate ad <c>AuthOptions</c> (sezione <c>Auth</c>) come
/// <c>AdminRoles</c> ed <c>EditorAccRoles</c>. La divisione dice <b>qual è</b> la divisione; <b>a chi</b> i
/// suoi codici danno un permesso è un'altra domanda, e stava qui solo perché i codici portano il prefisso
/// della divisione.</para>
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
}
