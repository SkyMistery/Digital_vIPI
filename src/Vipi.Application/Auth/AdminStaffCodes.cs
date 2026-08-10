using System.Text.RegularExpressions;

namespace Vipi.Application.Auth;

/// <summary>
/// I pattern degli staff code che valgono «admin»: unica fonte, usata sia da chi <b>decide</b>
/// (<see cref="EditAuthorizationService"/>) sia da chi <b>racconta</b> cosa sta succedendo (la diagnostica).
///
/// <para><b>Perché non è più dentro il costruttore dell'autorizzazione.</b> Una diagnosi che ricalcolasse i
/// pattern per conto proprio direbbe «va tutto bene» mentre l'autorizzazione ne usa altri: la diagnosi
/// perderebbe l'unica proprietà che la rende utile, cioè descrivere il sistema vero.</para>
/// </summary>
public static class AdminStaffCodes
{
    /// <summary>
    /// I pattern in vigore, come stringhe regex. Se <c>Auth:AdminStaffCodes</c> è valorizzato vince lui,
    /// completo; altrimenti si derivano dalla divisione: ruoli di divisione (<c>^IT-DIR$</c>) e ruoli chief
    /// ACC-scoped (<c>^LI[A-Z0-9]+-CH$</c>).
    /// </summary>
    public static IReadOnlyList<string> Patterns(AuthOptions auth, DivisionOptions division)
    {
        if (auth.AdminStaffCodes is { Count: > 0 } configurati) return configurati.ToList();

        var divRoles = division.AdminRolePatterns.Select(role => $"^{Regex.Escape(division.Code)}-{role}$");
        var accRoles = division.IcaoPrefixes.SelectMany(prefix =>
            division.AdminAccRolePatterns.Select(role => $"^{Regex.Escape(prefix)}[A-Z0-9]+-{role}$"));

        // Distinct perché il binder di configurazione AGGIUNGE alle liste di default invece di sostituirle:
        // `IcaoPrefixes: ["LI"]` in appsettings, sommato al default `["LI"]`, dà «LI» due volte e quindi ogni
        // pattern ACC duplicato. Innocuo per il confronto, ma la diagnostica mostra questi pattern a schermo e
        // un elenco con doppioni fa dubitare della configurazione invece che leggerla.
        return divRoles.Concat(accRoles).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>I pattern già compilati, come li usa il controllo di autorizzazione.</summary>
    public static Regex[] Compile(IReadOnlyList<string> patterns) => patterns
        .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
        .ToArray();

    /// <summary>Quali fra i codici dati combaciano con almeno un pattern (case-insensitive).</summary>
    public static IReadOnlyList<string> Matching(IEnumerable<string> codes, IReadOnlyList<Regex> compiled) =>
        codes.Where(c => !string.IsNullOrWhiteSpace(c) && compiled.Any(rx => rx.IsMatch(c))).ToList();
}
