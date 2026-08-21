using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;

namespace Vipi.Application.Auth;

/// <summary>Uno staffista visto dal roster, coi suoi codici e quali di questi valgono admin.</summary>
public sealed record AdminCodeRow(int UserId, string? DisplayName, IReadOnlyList<string> Codes,
    IReadOnlyList<string> Matched);

/// <summary>Fotografia di «chi può editare»: i pattern in vigore e i codici realmente osservati.</summary>
public sealed record AdminCoverage(IReadOnlyList<string> Patterns, IReadOnlyList<AdminCodeRow> Rows)
{
    /// <summary>Nessuno ha mai fatto login: il roster si popola dai login, quindi non si sa ancora nulla.</summary>
    public bool RosterEmpty => Rows.Count == 0;

    /// <summary>Almeno uno degli staffisti conosciuti è admin.</summary>
    public bool AnyAdmin => Rows.Any(r => r.Matched.Count > 0);

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
/// <para>I pattern (<c>^IT-DIR$</c>, <c>^LI[A-Z0-9]+-CH$</c>, …) sono <b>ipotesi</b>: solo il formato dei
/// ruoli di divisione è stato osservato davvero. Se sbagliano, i due modi di rompersi non si somigliano —
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
    private readonly IReadOnlyList<string> _patterns;

    public AdminCoverageService(IStaffRosterRepository roster, IOptions<AuthOptions> auth,
        IOptions<DivisionOptions> division)
    {
        _roster = roster;
        _patterns = AdminStaffCodes.Patterns(auth.Value, division.Value);
    }

    public async Task<AdminCoverage> DescribeAsync(CancellationToken ct = default)
    {
        var compilati = AdminStaffCodes.Compile(_patterns);
        var righe = (await _roster.ListActiveAsync(ct))
            .Select(s => new AdminCodeRow(s.UserId, s.DisplayName, s.StaffPositions,
                AdminStaffCodes.Matching(s.StaffPositions, compilati)))
            .ToList();

        return new AdminCoverage(_patterns, righe);
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
                $"Codici visti e non riconosciuti: {visti}. Finché è così nessuno può editare né assegnare permessi, " +
                "e la cosa non si sblocca da dentro: si corregge «Auth:AdminStaffCodes» o la sezione «Division».",
                // ⚠️ Nessuna rotta: questo NON si ripara da dentro l'applicazione — è proprio ciò che il
                // rilievo dice. Mandare a /vsop/admin/permessi sarebbe mandare a una porta chiusa.
                ConsistencyArea.Configurazione),
        };
    }
}
