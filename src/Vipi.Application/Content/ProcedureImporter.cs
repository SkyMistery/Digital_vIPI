using System.Collections.Concurrent;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IProcedureImporter"/>
public sealed class ProcedureImporter : IProcedureImporter
{
    // Serializza gli import sullo stesso aeroporto (job periodico + bottone editor): ReplaceImportedProceduresAsync fa
    // delete+add, quindi due run concorrenti tenterebbero di scrivere due volte le stesse righe.
    //
    // ATTENZIONE: è un lock DI PROCESSO, e copre il deploy attuale (Render, istanza singola) ma non due repliche.
    // Non si può rafforzare con un indice unico su (AirportId, StableKey): quella chiave esclude di proposito la
    // cifra della revisione ed è legittimamente ripetuta quando il file .sid contiene due revisioni della stessa
    // SID. Se si passerà a più istanze servirà un lock condiviso (advisory lock DB), non un vincolo di unicità.
    // Il dizionario è limitato dal numero di aeroporti in catalogo (decine), quindi non richiede sfoltimento.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    private readonly IProcedureProvider _provider;
    private readonly IAirportRepository _repo;
    private readonly IImportPolicyStore _policy;
    private readonly IAiracService _airac;
    private readonly Vipi.Application.Auth.IEditAuthorizationService _authz;

    /// <summary>Che cosa dice di sé la sorgente. Opzionale: senza, il ciclo d'entrata scende ai ripieghi di
    /// <see cref="SidStampCycle"/> — cioè al comportamento di prima della carta §AW2.</summary>
    private readonly ISidSourceRelease? _sorgente;

    /// <summary>L'ultimo giro riuscito, ultimo ripiego. Opzionale come sopra.</summary>
    private readonly IImportStateStore? _stati;

    public ProcedureImporter(IProcedureProvider provider, IAirportRepository repo, IImportPolicyStore policy,
        IAiracService airac, Vipi.Application.Auth.IEditAuthorizationService authz,
        ISidSourceRelease? sorgente = null, IImportStateStore? stati = null)
    {
        _provider = provider;
        _repo = repo;
        _policy = policy;
        _airac = airac;
        _authz = authz;
        _sorgente = sorgente;
        _stati = stati;
    }

    /// <inheritdoc />
    public async Task<int> ImportForCurrentUserAsync(string icao, CancellationToken ct = default)
    {
        var norm = icao.Trim().ToUpperInvariant();
        var acc = await _repo.GetAccCodeByIcaoAsync(norm, ct)
            ?? throw new Vipi.Application.Aor.ValidationException(Lingua($"Aeroporto {norm} inesistente o senza ACC.", $"Airport {norm} does not exist, or has no ACC."));
        _authz.EnsureAtLeast(VipiRole.Editor);
        return await ImportAsync(norm, ct);
    }

    /// <inheritdoc />
    public async Task<int> ImportAsync(string icao, CancellationToken ct = default)
    {
        icao = icao.Trim().ToUpperInvariant();
        var policy = await _policy.GetAsync(ct);
        if (!policy.IsImported(ImportCategory.Sids)) return 0;   // categoria disattivata: non toccare le procedure

        // ⚠️ Si scrive il ciclo DAL QUALE la riga vale, e lo dichiara la SORGENTE: non è più «il ciclo in
        // cui è capitato di girare» più uno. Il giro è ogni 24 ore, con ritardo d'avvio e ritentativi, e da
        // quel valore dipendeva di un MESE quando la SID diventa pubblica (SidRow.IsPublicAt). I tre gradini
        // — e perché i ripieghi sbagliano apposta in avanti — stanno in SidStampCycle. Carta §AW2.
        //
        // Si calcola UNA volta per i due versi: vengono dallo stesso sectorfile, quindi dallo stesso ciclo.
        var cycle = SidStampCycle.Scegli(
            _airac, DateTime.UtcNow,
            _sorgente is null ? SidSourceRelease.Muta : await _sorgente.ReadAsync(ct),
            _stati is null ? null : await _stati.GetLastSuccessAsync(ImportCategories.Sid, ct));

        var scritte = 0;
        foreach (var kind in new[] { ProcedureKind.Sid, ProcedureKind.Star })
            scritte += await ImportaVersoAsync(icao, kind, cycle, ct);
        return scritte;
    }

    /// <summary>
    /// Un verso solo. ⚠️ <b>Zero righe dalla sorgente = non si tocca niente</b>: il file può mancare (36 dei 90
    /// <c>.str</c> non portano nemmeno una STAR), la rete può essere caduta, e in nessuno dei due casi «non è
    /// arrivato niente» significa «non c'è più niente». Un <c>ReplaceImported…</c> con la lista vuota
    /// cancellerebbe le importate esistenti.
    /// </summary>
    private async Task<int> ImportaVersoAsync(string icao, ProcedureKind kind, string cycle, CancellationToken ct)
    {
        var source = await _provider.GetAsync(icao, kind, ct);
        if (source.Count == 0) return 0;

        var rows = source.Select(s => new ImportedProcedure(
            Runway: s.Runway, Fix: s.Fix, Name: s.Name, Transition: s.Transition,
            Type: s.Type, StableKey: s.StableKey, NeedsFixReview: s.NeedsFixReview)).ToList();

        // Solo la scrittura DB è serializzata (il fetch di rete resta concorrente): due run finiscono per riscrivere
        // gli stessi dati in sequenza (idempotente), senza duplicare righe.
        // ⚠️ Il lucchetto è per AEROPORTO e non per (aeroporto, verso): i due versi dello stesso scalo scrivono
        // nella stessa tabella, e due giri concorrenti si passerebbero davanti a metà merge.
        var gate = _locks.GetOrAdd(icao, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { await _repo.ReplaceImportedProceduresAsync(icao, kind, rows, cycle, ct); }
        finally { gate.Release(); }
        return rows.Count;
    }
}
