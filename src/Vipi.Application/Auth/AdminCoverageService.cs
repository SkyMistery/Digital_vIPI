using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;
using Vipi.Domain;

namespace Vipi.Application.Auth;

/// <summary>
/// Uno staffista visto dal roster: i suoi codici, quali di questi valgono admin, e il <b>livello effettivo</b>
/// — che può venire da una promozione a mano e non dai codici.
/// </summary>
public sealed record AdminCodeRow(int UserId, string? DisplayName, IReadOnlyList<string> Codes,
    IReadOnlyList<string> Matched, VipiRole Level, bool Promosso);

/// <summary>Fotografia di «chi può editare»: i pattern in vigore, per livello, e i codici osservati.</summary>
public sealed record AdminCoverage(
    IReadOnlyList<string> Patterns,
    IReadOnlyList<string> EditorPatterns,
    IReadOnlyList<string> DivisionStaffPatterns,
    IReadOnlyList<AdminCodeRow> Rows)
{
    /// <summary>Nessuno ha mai fatto login: il roster si popola dai login, quindi non si sa ancora nulla.</summary>
    public bool RosterEmpty => Rows.Count == 0;

    /// <summary>
    /// Almeno uno degli staffisti conosciuti è admin. ⚠️ Guarda il livello <b>effettivo</b>, non i codici:
    /// un admin per promozione a mano è un admin, e un rilievo che lo ignorasse manderebbe a cercare un
    /// guasto che non c'è.
    /// </summary>
    public bool AnyAdmin => Rows.Any(r => r.Level >= VipiRole.Admin);

    /// <summary>Codici osservati che NON valgono admin: è l'elenco da cui capire se un pattern è sbagliato.</summary>
    public IReadOnlyList<string> UnmatchedCodes => Rows
        .SelectMany(r => r.Codes.Except(r.Matched, StringComparer.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

/// <summary>
/// Risponde a una domanda che finora nessuno poneva: <b>i codici admin configurati corrispondono a quelli
/// veri di IVAO?</b>
///
/// <para>Dal 28 agosto 2026 admin sono <b>otto codici puntuali</b> di direzione (<c>IT-DIR</c>,
/// <c>IT-ADIR</c>, <c>IT-WM</c>, <c>IT-AWM</c>, <c>IT-AOC</c>, <c>IT-AOAC</c>, <c>IT-SOC</c>,
/// <c>IT-SOAC</c>) più i fondatori per VID; il resto dello staff <c>IT-</c> è <c>DivisionStaff</c> e i
/// chief d'ACC sono <c>Editor</c>. ⚠️ Questa diagnosi guarda <b>solo il livello admin</b>, che è quello
/// che, mancando, non si ripara da dentro. Se sbaglia, i due modi di rompersi non si somigliano —
/// <b>nessuno è admin</b> significa che in produzione nessuno può editare nulla e non lo si può nemmeno
/// rimediare da dentro (distribuire i permessi richiede di essere admin); <b>troppi admin</b> significa dare
/// il controllo editoriale a chi non doveva averlo. Il primo è silenzioso, il secondo lo è ancora di più.</para>
///
/// <para>Non si può chiedere a IVAO l'elenco degli staffisti (<c>/v2/divisions/{id}/members</c> è 404 col
/// token app), ma il roster si popola <b>dai login</b>: dopo qualche accesso i codici veri sono lì, e questa
/// diagnosi li mette a confronto coi pattern. È una risposta empirica a una domanda che altrimenti resta
/// un'opinione.</para>
/// </summary>
public interface IAdminCoverageService
{
    /// <summary>Pattern in vigore e codici osservati, per la pagina di diagnostica.</summary>
    Task<AdminCoverage> DescribeAsync(CancellationToken ct = default);

    /// <summary>Il rilievo per il report di consistenza e per l'health check. Vuoto se non c'è nulla da dire.</summary>
    Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAdminCoverageService"/>
public sealed class AdminCoverageService : IAdminCoverageService
{
    private readonly IStaffRosterRepository _roster;
    private readonly RoleResolver _resolver;
    private readonly IRoleOverrides _promozioni;

    // I pattern non si ricalcolano qui: sono quelli del RoleResolver, cioè gli stessi che l'autorizzazione
    // usa davvero. Una diagnosi che se li ricostruisse per conto proprio potrebbe dire «va tutto bene»
    // mentre il prodotto ne applica altri — e perderebbe l'unica proprietà che la rende utile.
    public AdminCoverageService(IStaffRosterRepository roster, RoleResolver resolver, IRoleOverrides promozioni)
    {
        _roster = roster;
        _resolver = resolver;
        _promozioni = promozioni;
    }

    public async Task<AdminCoverage> DescribeAsync(CancellationToken ct = default)
    {
        var righe = (await _roster.ListActiveAsync(ct))
            .Select(s =>
            {
                // ⚠️ Il livello che si mostra è quello EFFETTIVO, promozione compresa: una diagnosi che
                // guardasse i soli codici direbbe «nessuno è admin» mentre qualcuno lo è per promozione, e
                // manderebbe a caccia di un guasto che non c'è.
                var promozione = _promozioni.For(s.UserId);
                return new AdminCodeRow(
                    s.UserId, s.DisplayName, s.StaffPositions,
                    _resolver.MatchingCodes(s.StaffPositions, VipiRole.Admin),
                    _resolver.Effective(s.UserId, s.StaffPositions, promozione),
                    Promosso: promozione is { } p && p > _resolver.Resolve(s.UserId, s.StaffPositions));
            })
            .ToList();

        return new AdminCoverage(
            _resolver.AdminPatterns, _resolver.EditorPatterns, _resolver.DivisionStaffPatterns, righe);
    }

    public async Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default)
    {
        var c = await DescribeAsync(ct);

        // Roster vuoto = nessuno ha ancora fatto login: su un'installazione appena nata è normale, non un
        // guasto. Segnalarlo qui riempirebbe di rumore proprio il momento in cui il rumore non serve.
        if (c.RosterEmpty || c.AnyAdmin) return Array.Empty<ConsistencyFinding>();

        var visti = c.UnmatchedCodes.Count == 0 ? "nessuno" : string.Join(", ", c.UnmatchedCodes);
        return new[]
        {
            new ConsistencyFinding("Nessun admin fra gli staffisti conosciuti", ConsistencySeverity.Error,
                $"{c.Rows.Count} staffisti nel roster",
                $"Nessuno dei codici staff osservati combacia coi pattern admin in vigore ({string.Join(" | ", c.Patterns)}). " +
                $"Codici visti e non riconosciuti: {visti}. Finché è così nessuno può assegnare permessi, " +
                "e la cosa non si sblocca da dentro: si corregge «Auth:AdminRoles» (o «Auth:AdminStaffCodes» " +
                "per sostituirli tutti), oppure si mette un VID in «Auth:FounderVids».",
                // ⚠️ Nessuna rotta: questo NON si ripara da dentro l'applicazione — è proprio ciò che il
                // rilievo dice. Mandare a /services/vsop/admin/permissions sarebbe mandare a una porta chiusa.
                ConsistencyArea.Configurazione,
                CategoryKey: "Diag_Cat_NessunAdmin", DetailKey: "Diag_Msg_NessunAdmin",
                DetailArgs: new object[] { string.Join(" | ", c.Patterns), visti },
                EntityKey: "Diag_Ent_Staffisti", EntityArgs: new object[] { c.Rows.Count }),
        };
    }
}
