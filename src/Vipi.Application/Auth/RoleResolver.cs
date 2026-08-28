using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Auth;

/// <summary>
/// Da «chi sei per IVAO» a «cosa puoi fare qui»: le posizioni staff del portale diventano un
/// <see cref="VipiRole"/>. Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §4.
///
/// <para><b>È una funzione pura, e deve restarlo.</b> Niente database, niente <c>HttpContext</c>, niente
/// orologio: gli stessi codici danno sempre lo stesso livello. È ciò che permette di provarlo per tabella
/// di verità invece che guidando un browser — e il permesso più alto del prodotto è esattamente la cosa
/// che non si vuole verificare a schermo.</para>
///
/// <para><b>Perché una classe e non tre metodi statici.</b> I pattern si compilano una volta sola: sono
/// otto regex per l'admin, due per ogni prefisso ICAO, una per la divisione, e la domanda «che livello ha
/// questa persona?» arriva a ogni richiesta. Il servizio è registrato singleton.</para>
///
/// <para>⚠️ <b>L'override per VID non sta qui.</b> Questo risponde solo «quanto ti dà la tua posizione
/// staff», cioè il <b>pavimento</b>. La promozione a mano è un'altra cosa e vive in banca dati: il livello
/// effettivo è <c>max(qui, override)</c>, e lo compone chi ha accesso a entrambi.</para>
/// </summary>
public sealed class RoleResolver
{
    private readonly Regex[] _admin;
    private readonly Regex[] _editor;
    private readonly Regex[] _divisionStaff;
    private readonly HashSet<int> _founders;

    public RoleResolver(AuthOptions auth, DivisionOptions division)
    {
        AdminPatterns = BuildAdminPatterns(auth, division);
        EditorPatterns = BuildEditorPatterns(auth, division);
        DivisionStaffPatterns = BuildDivisionStaffPatterns(division);

        _admin = Compile(AdminPatterns);
        _editor = Compile(EditorPatterns);
        _divisionStaff = Compile(DivisionStaffPatterns);
        _founders = auth.FounderVids.Where(v => v > 0).ToHashSet();
    }

    public RoleResolver(IOptions<AuthOptions> auth, IOptions<DivisionOptions> division)
        : this(auth.Value, division.Value) { }

    /// <summary>I pattern in vigore, come stringhe: la diagnostica li mostra a schermo.</summary>
    public IReadOnlyList<string> AdminPatterns { get; }

    /// <inheritdoc cref="AdminPatterns"/>
    public IReadOnlyList<string> EditorPatterns { get; }

    /// <inheritdoc cref="AdminPatterns"/>
    public IReadOnlyList<string> DivisionStaffPatterns { get; }

    /// <summary>I VID che sono <see cref="VipiRole.Admin"/> comunque, qualunque posizione staff abbiano.</summary>
    public IReadOnlySet<int> Founders => _founders;

    /// <summary>Il livello garantito dall'identità IVAO. Utente nullo (anonimo) = <see cref="VipiRole.User"/>.</summary>
    public VipiRole Resolve(CurrentUser? user) =>
        user is null ? VipiRole.User : Resolve(user.UserId, user.StaffPositions);

    /// <summary>
    /// Il livello garantito da un VID e dalle sue posizioni staff.
    ///
    /// <para>Si valuta <b>dall'alto</b> e vince il primo che risponde: chi ha più posizioni prende la più
    /// alta. Un <c>IT-DIR</c> combacia anche col pattern dello staff di divisione, ed è giusto così —
    /// l'ordine, non i pattern, decide che è admin.</para>
    /// </summary>
    public VipiRole Resolve(int userId, IEnumerable<string>? staffPositions)
    {
        if (_founders.Contains(userId)) return VipiRole.Admin;

        // Materializzata una volta: la si attraversa fino a quattro volte, e a monte è spesso il risultato
        // del parse di un array JSON di claim.
        var codes = (staffPositions ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToArray();

        if (codes.Length == 0) return VipiRole.User;
        if (Matches(codes, _admin)) return VipiRole.Admin;
        if (Matches(codes, _editor)) return VipiRole.Editor;
        if (Matches(codes, _divisionStaff)) return VipiRole.DivisionStaff;
        return VipiRole.IvaoStaff;
    }

    /// <summary>
    /// Il livello <b>effettivo</b>: <c>max</c> fra quello garantito dall'identità IVAO e la promozione
    /// scritta a mano.
    ///
    /// <para><b>Il pavimento non è un controllo: è questo <c>max</c>.</b> «Nessuno si declassa sotto ciò
    /// che la sua posizione staff gli garantisce» non è una regola da scrivere e da ricordarsi di
    /// applicare — è ciò che <c>max</c> fa già, e in un posto solo. Un declassamento sotto il pavimento
    /// non è vietato: è <b>inerte</b>.</para>
    ///
    /// <para>⚠️ Siccome è inerte <b>e silenzioso</b>, la pagina che assegna i livelli deve mostrare
    /// disabilitati quelli sotto il pavimento: un comando che accetta e non fa niente è peggio di un
    /// comando che non c'è.</para>
    /// </summary>
    public VipiRole Effective(CurrentUser? user, VipiRole? overrideLevel)
    {
        var daStaff = Resolve(user);
        return overrideLevel is { } o && o > daStaff ? o : daStaff;
    }

    /// <inheritdoc cref="Effective(CurrentUser?, VipiRole?)"/>
    public VipiRole Effective(int userId, IEnumerable<string>? staffPositions, VipiRole? overrideLevel)
    {
        var daStaff = Resolve(userId, staffPositions);
        return overrideLevel is { } o && o > daStaff ? o : daStaff;
    }

    /// <summary>Quali fra i codici dati fanno scattare un livello: serve alla diagnostica per dire <i>perché</i>.</summary>
    public IReadOnlyList<string> MatchingCodes(IEnumerable<string> codes, VipiRole level)
    {
        var compiled = level switch
        {
            VipiRole.Admin => _admin,
            VipiRole.Editor => _editor,
            VipiRole.DivisionStaff => _divisionStaff,
            _ => Array.Empty<Regex>(),
        };
        return codes.Where(c => !string.IsNullOrWhiteSpace(c) && compiled.Any(rx => rx.IsMatch(c.Trim()))).ToList();
    }

    private static bool Matches(IReadOnlyList<string> codes, Regex[] compiled) =>
        compiled.Length > 0 && codes.Any(c => compiled.Any(rx => rx.IsMatch(c)));

    /// <summary>
    /// I codici di direzione della divisione: <c>^IT-(DIR|ADIR|WM|AWM|AOC|AOAC|SOC|SOAC)$</c>.
    ///
    /// <para>⚠️ <b>È un elenco puntuale, e il difetto dell'elenco puntuale torna con lui</b>: un ruolo di
    /// direzione nuovo — poniamo <c>IT-ATOC</c> — nascerà <see cref="VipiRole.DivisionStaff"/>, non admin.
    /// Il 22 agosto 2026 si era scelto il jolly proprio per questo; il 28 il committente ha scelto
    /// l'elenco, e il compromesso regge per una ragione precisa: <b>adesso esiste la promozione a mano</b>,
    /// quindi il caso non previsto si ripara da dentro il prodotto in trenta secondi, mentre un admin di
    /// troppo non si scopre finché non fa danni.</para>
    ///
    /// <para><c>Auth:AdminStaffCodes</c>, se valorizzato, <b>sostituisce tutto</b> con pattern completi: è
    /// la via per restringere, perché il binder della configurazione <i>aggiunge</i> alle liste di default
    /// invece di sostituirle.</para>
    /// </summary>
    private static IReadOnlyList<string> BuildAdminPatterns(AuthOptions auth, DivisionOptions division)
    {
        if (auth.AdminStaffCodes is { Count: > 0 } espliciti) return espliciti.Distinct(StringComparer.Ordinal).ToList();

        return auth.AdminRoles
            .Select(role => $"^{Regex.Escape(division.Code)}-{role}$")
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// I chief d'ACC: <c>^LI[A-Z0-9]+-(CH|ACH)$</c>, un blocco per prefisso ICAO della divisione.
    ///
    /// <para>⚠️ Il codice di un chief ha il prefisso <b>dell'ACC</b>, non quello della divisione, ed è per
    /// questo che non basta il pattern dello staff di divisione a pescarlo. <c>Distinct</c> perché il
    /// binder somma <c>IcaoPrefixes</c> ai default: <c>["LI"]</c> in appsettings più <c>["LI"]</c> di
    /// default dà «LI» due volte, e la diagnostica mostra questi pattern a schermo.</para>
    /// </summary>
    private static IReadOnlyList<string> BuildEditorPatterns(AuthOptions auth, DivisionOptions division) =>
        division.IcaoPrefixes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(prefix => auth.EditorAccRoles
                .Select(role => $"^{Regex.Escape(prefix)}[A-Z0-9]+-{role}$"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Tutto il resto dello staff della divisione: <c>^IT-[A-Z0-9]+$</c>.
    ///
    /// <para>Il jolly qui è al posto giusto e non allarga oltre la divisione: un codice
    /// <c>{Code}-{ruolo}</c> lo assegna il portale IVAO solo al suo staff, e il prefisso resta la barriera
    /// (<c>DE-DIR</c> non è staff italiano).</para>
    /// </summary>
    private static IReadOnlyList<string> BuildDivisionStaffPatterns(DivisionOptions division) =>
        new[] { $"^{Regex.Escape(division.Code)}-[A-Z0-9]+$" };

    private static Regex[] Compile(IReadOnlyList<string> patterns) => patterns
        .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
        .ToArray();
}
