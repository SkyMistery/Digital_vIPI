using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Tutti gli elenchi delle configurazioni possibili, letti in un colpo.
///
/// <para>Sta in una classe sua perché la chiedono in due: il servizio della Struttura e
/// <c>TopologyBuilder</c>, che la mette nella <see cref="Vipi.Application.Aor.Topology"/> accanto ai ripieghi.
/// La tabella ha una riga per gruppo (una ventina in tutta la divisione): niente da paginare.</para>
/// </summary>
internal static class ConfigurazioniQuery
{
    public static async Task<ConfigurazioniPossibili> TutteAsync(VipiDbContext db, CancellationToken ct)
    {
        var righe = await db.SectorConfigurationSets.AsNoTracking()
            .OrderBy(r => r.GroupKind).ThenBy(r => r.GroupCode)
            .Select(r => new { r.GroupKind, r.GroupCode, r.BodyJson, r.IsExhaustive })
            .ToListAsync(ct);
        // ⚠️ Solo gli elenchi COMPLETI vincolano (ConfigurazioniPossibili li filtra da sé): gli altri si leggono
        // comunque, perché costa una riga e il filtro deve stare in un posto solo.
        if (!righe.Any(r => r.IsExhaustive)) return ConfigurazioniPossibili.Nessuna;

        return new ConfigurazioniPossibili(righe.Select(r =>
            new ElencoDiConfigurazioni(r.GroupKind, r.GroupCode, ConfigurazioniJson.Leggi(r.BodyJson), r.IsExhaustive)));
    }
}

/// <inheritdoc cref="ISectorConfigurationService"/>
public sealed class EfSectorConfigurationService : ISectorConfigurationService
{
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly IResourceLockService _locks;

    public EfSectorConfigurationService(VipiDbContext db, IEditAuthorizationService authz, IResourceLockService locks)
    {
        _db = db;
        _authz = authz;
        _locks = locks;
    }

    public Task<ConfigurazioniPossibili> TutteAsync(CancellationToken ct = default) =>
        ConfigurazioniQuery.TutteAsync(_db, ct);

    public async Task<IReadOnlyList<AccConfiguration>> ListAsync(
        ConfigurationGroupKind genere, string codice, CancellationToken ct = default)
    {
        var codiceNorm = Norm(codice);
        var json = await _db.SectorConfigurationSets.AsNoTracking()
            .Where(r => r.GroupKind == genere && r.GroupCode == codiceNorm)
            .Select(r => r.BodyJson)
            .FirstOrDefaultAsync(ct);
        return ConfigurazioniJson.Leggi(json);
    }

    public async Task<IReadOnlyList<GruppoDiSettori>> GruppiAsync(string accCode, CancellationToken ct = default)
    {
        var acc = Norm(accCode);
        var settori = await SettoriAttiviAsync(ct);
        var scritti = (await _db.SectorConfigurationSets.AsNoTracking()
                .Select(r => new { r.GroupKind, r.GroupCode, r.BodyJson, r.IsExhaustive }).ToListAsync(ct))
            .ToDictionary(r => (r.GroupKind, r.GroupCode.ToUpperInvariant()), r => (r.BodyJson, r.IsExhaustive));
        List<AccConfiguration> ElencoDi(ConfigurationGroupKind g, string c) =>
            ConfigurazioniJson.Leggi(scritti.GetValueOrDefault((g, c.ToUpperInvariant())).BodyJson);
        bool Completo(ConfigurationGroupKind g, string c) =>
            scritti.GetValueOrDefault((g, c.ToUpperInvariant())).IsExhaustive;

        var gruppi = new List<GruppoDiSettori>();

        var area = SettoriDArea(settori, acc);
        if (area.Count > 0)
            gruppi.Add(new GruppoDiSettori(ConfigurationGroupKind.AccArea, acc, acc, area,
                ElencoDi(ConfigurationGroupKind.AccArea, acc), Completo(ConfigurationGroupKind.AccArea, acc)));

        var enti = await _db.AtcUnits.AsNoTracking()
            .Where(u => u.Acc!.Code == acc)
            .OrderBy(u => u.Name).ThenBy(u => u.Code)
            .Select(u => new { u.Code, u.Name, Posizioni = u.Positions.OrderBy(p => p.Order).Select(p => p.Callsign).ToList() })
            .ToListAsync(ct);
        foreach (var e in enti)
            gruppi.Add(new GruppoDiSettori(ConfigurationGroupKind.AtcUnit, e.Code, e.Name,
                SettoriDellEnte(settori, e.Posizioni), ElencoDi(ConfigurationGroupKind.AtcUnit, e.Code),
                Completo(ConfigurationGroupKind.AtcUnit, e.Code)));

        return gruppi;
    }

    public async Task ReplaceAsync(ConfigurationGroupKind genere, string codice,
        IReadOnlyList<AccConfiguration> configurazioni, bool completo, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        // È struttura quanto i ripieghi (T-025): si scrive sotto lo stesso lock.
        await _locks.EnsureHeldAsync(ResourceLockKeys.Structure, ct);

        var codiceNorm = Norm(codice);
        var ammessi = await SettoriDelGruppoAsync(genere, codiceNorm, ct)
            ?? throw new ValidationException(Lingua(
                $"Il gruppo «{codiceNorm}» non esiste.", $"Group «{codiceNorm}» does not exist."));
        var perNome = ammessi.ToDictionary(s => s.Callsign, s => s.Callsign, OIC);

        var pulite = new List<AccConfiguration>();
        foreach (var c in configurazioni ?? Array.Empty<AccConfiguration>())
        {
            var nome = string.IsNullOrWhiteSpace(c.Name) ? $"Conf {pulite.Count + 1}" : c.Name.Trim();
            var aperti = new List<AccConfigOpen>();
            foreach (var o in c.Open)
            {
                var cs = o.Callsign?.Trim();
                if (string.IsNullOrEmpty(cs)) continue;
                if (!perNome.TryGetValue(cs, out var canonico))
                    throw new ValidationException(Lingua(
                        $"«{cs}» non è un settore del gruppo «{codiceNorm}» (configurazione «{nome}»).",
                        $"«{cs}» is not a sector of group «{codiceNorm}» (configuration «{nome}»)."));
                if (aperti.Any(a => OIC.Equals(a.Callsign, canonico))) continue;   // due volte lo stesso: una
                aperti.Add(new AccConfigOpen
                {
                    Callsign = canonico,
                    CenterPoint = string.IsNullOrWhiteSpace(o.CenterPoint) ? null : o.CenterPoint.Trim(),
                    Range = string.IsNullOrWhiteSpace(o.Range) ? null : o.Range.Trim(),
                });
            }

            pulite.Add(new AccConfiguration
            {
                Key = string.IsNullOrWhiteSpace(c.Key) ? "cfg:" + Guid.NewGuid().ToString("N")[..8] : c.Key.Trim(),
                Name = nome,
                Open = aperti,
            });
        }

        var riga = await _db.SectorConfigurationSets
            .FirstOrDefaultAsync(r => r.GroupKind == genere && r.GroupCode == codiceNorm, ct);
        if (riga is null)
            _db.SectorConfigurationSets.Add(riga = new SectorConfigurationSet { GroupKind = genere, GroupCode = codiceNorm });
        // ⚠️ La riga resta anche con l'elenco vuoto: dice che per questo gruppo si è già deciso, e il travaso
        // dal documento non deve riportare indietro quel che qui è stato tolto.
        riga.BodyJson = ConfigurazioniJson.Scrivi(pulite);
        // Un elenco senza nemmeno un settore aperto non può essere «completo»: vorrebbe dire che il gruppo non
        // apre mai, e non è quello che ha scritto chi ha lasciato una riga vuota.
        riga.IsExhaustive = completo && pulite.Any(p => p.Open.Count > 0);
        riga.UpdatedAtUtc = DateTime.UtcNow;

        AuditScribe.Write(_db, _authz.CurrentUserId ?? 0, AuditAction.HierarchyChange, nameof(SectorConfigurationSet),
            codiceNorm,
            new
            {
                Gruppo = codiceNorm,
                Genere = genere.ToString(),
                Completo = riga.IsExhaustive,
                Configurazioni = pulite.Select(p => $"{p.Name}: {string.Join(" + ", p.OpenCallsigns)}").ToList(),
            });

        await _db.SaveChangesAsync(ct);
    }

    // ---- i settori di un gruppo ----

    private sealed record SettoreAttivo(string Callsign, string Name, SectorType Type, string AccCode, string? Padre);

    private async Task<List<SettoreAttivo>> SettoriAttiviAsync(CancellationToken ct) =>
        (await _db.Sectors.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new
            {
                s.Callsign, s.Name, s.Type, Acc = s.Acc!.Code,
                Padre = s.ParentSector != null ? s.ParentSector.Callsign : null,
            })
            .ToListAsync(ct))
        .Select(s => new SettoreAttivo(s.Callsign, s.Name, s.Type, s.Acc, s.Padre))
        .ToList();

    /// <summary>
    /// I settori d'area <b>ordinari</b> dell'ACC. ⚠️ Senza MIL e FSS: hanno le loro sezioni nel documento e non
    /// stanno nelle configurazioni (committente, 21 settembre 2026) — e un elenco che non li nomina li lascia
    /// liberi, che è quel che serve.
    /// </summary>
    private static List<AccSectorPick> SettoriDArea(IEnumerable<SettoreAttivo> settori, string acc) =>
        settori
            .Where(s => s.Type == SectorType.Ctr && OIC.Equals(s.AccCode, acc)
                        && AccFamigliaAorRegola.Di(s.Callsign) == FamigliaAor.Ordinaria)
            .OrderBy(s => s.Callsign, OIC)
            .Select(s => new AccSectorPick(s.Callsign, s.Name))
            .ToList();

    /// <summary>
    /// Le posizioni dell'ente, nel loro ordine, e dopo gli avvicinamenti che stanno sotto di loro: lo stesso
    /// insieme da cui il documento dell'ente sceglieva (le posizioni entrano sempre, di qualunque tipo siano —
    /// a Pratica è una torre).
    /// </summary>
    private static List<AccSectorPick> SettoriDellEnte(IReadOnlyList<SettoreAttivo> settori, IReadOnlyList<string> posizioni)
    {
        var perNome = settori.ToDictionary(s => s.Callsign, OIC);
        var scelti = new List<AccSectorPick>();
        var visti = new HashSet<string>(OIC);
        foreach (var p in posizioni)
            if (visti.Add(p))
                scelti.Add(new AccSectorPick(perNome.TryGetValue(p, out var s) ? s.Callsign : p, s?.Name ?? p));

        // I discendenti APP: chiusura a punto fisso sui padri, come Topology.DomainOf.
        var dominio = new HashSet<string>(posizioni, OIC);
        for (var aggiunto = true; aggiunto;)
        {
            aggiunto = false;
            foreach (var s in settori)
                if (s.Padre is not null && dominio.Contains(s.Padre) && dominio.Add(s.Callsign)) aggiunto = true;
        }
        foreach (var s in settori.Where(s => s.Type == SectorType.App && dominio.Contains(s.Callsign))
                     .OrderBy(s => s.Callsign, OIC))
            if (visti.Add(s.Callsign)) scelti.Add(new AccSectorPick(s.Callsign, s.Name));
        return scelti;
    }

    /// <summary>I settori fra cui un gruppo può scegliere; <c>null</c> se il gruppo non esiste.</summary>
    private async Task<List<AccSectorPick>?> SettoriDelGruppoAsync(
        ConfigurationGroupKind genere, string codice, CancellationToken ct)
    {
        var settori = await SettoriAttiviAsync(ct);
        if (genere == ConfigurationGroupKind.AccArea)
            return await _db.Accs.AsNoTracking().AnyAsync(a => a.Code == codice, ct) ? SettoriDArea(settori, codice) : null;

        var ente = await _db.AtcUnits.AsNoTracking()
            .Where(u => u.Code == codice)
            .Select(u => new { Posizioni = u.Positions.OrderBy(p => p.Order).Select(p => p.Callsign).ToList() })
            .FirstOrDefaultAsync(ct);
        return ente is null ? null : SettoriDellEnte(settori, ente.Posizioni);
    }

    private static string Norm(string? codice) => (codice ?? "").Trim().ToUpperInvariant();
}
