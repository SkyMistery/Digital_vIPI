using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Stats;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Quel che serve a sciogliere un <see cref="FallbackTargetKind.Coverage"/>, montato <b>una volta per
/// richiesta</b> e poi puro: volumi, chi è online, dove stanno i punti, e la topologia.
///
/// <para><b>Perché un oggetto e non quattro parametri.</b> Le pretese (<see cref="SectorVolumeMap.BuildClaims"/>)
/// costano un giro su tutti i settori e vanno costruite <b>per quota</b>, non per punto: una vista di
/// trasferimenti ha decine di punti e una manciata di quote distinte. Qui dentro si tengono in cache, e chi
/// risolve non deve saperlo.</para>
///
/// <para>🔴 <b>Il collasso usa la CATENA, non i soli padri.</b> È il vincolo che tiene allineate geometria e
/// ricaduta: <c>CoverageResolver.Owners</c> non conosce le righe dichiarate, e con <c>ES5</c> chiuso a FL350
/// risponderebbe <c>ES2</c> dove la catena risponde <c>WS5</c>. ⚠️ E dentro quel collasso i rinvii
/// <b>non</b> si consultano: un giro, e termina.</para>
/// </summary>
public sealed class CoverageFallbackContext
{
    /// <summary>Rinvii spenti: <see cref="Risolvi"/> risponde sempre «non è un punto» e nessuno se ne
    /// accorge, perché a tabella senza rinvii nessuno lo chiama.</summary>
    public static readonly CoverageFallbackContext Nessuno =
        new(Array.Empty<SectorVolumeRow>(), new HashSet<string>(), CopPositions.Empty,
            new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase), _ => null);

    private readonly IReadOnlyList<SectorVolumeRow> _settori;
    private readonly IReadOnlySet<string> _online;
    private readonly CopPositions _punti;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> _dichiarate;
    private readonly Func<string, string?> _padreDi;

    private readonly Dictionary<string, SectorVolumeRow> _perCallsign;
    private readonly Dictionary<int, IReadOnlyList<SectorClaim>> _claimsPerQuota = new();
    private readonly Dictionary<string, IReadOnlySet<string>> _dominii =
        new(StringComparer.OrdinalIgnoreCase);

    public CoverageFallbackContext(
        IReadOnlyList<SectorVolumeRow> settori,
        IReadOnlySet<string> online,
        CopPositions punti,
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> dichiarate,
        Func<string, string?> padreDi)
    {
        _settori = settori;
        _online = online;
        _punti = punti;
        _dichiarate = dichiarate;
        _padreDi = padreDi;
        _perCallsign = settori
            .GroupBy(s => s.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Il contesto di una <see cref="Topology"/>: le righe dichiarate e i padri vengono da lì.</summary>
    public static CoverageFallbackContext Da(
        Topology topologia, IReadOnlyList<SectorVolumeRow> settori, IReadOnlySet<string> online, CopPositions punti) =>
        new(settori, online, punti, topologia.Fallbacks, topologia.ParentOf);

    /// <summary>
    /// Lo stesso contesto con un <b>altro</b> insieme di stazioni online: volumi, punti e topologia si
    /// riusano, le pretese si ricalcolano.
    ///
    /// <para>⚠️ Serve alla <b>scala di risalita</b>, che simula per eliminazione: chiude il vincitore e
    /// richiede. Le pretese dipendono da chi è online — è il motivo per cui non si possono riusare, ed è
    /// anche il motivo per cui la cache è per QUOTA e non globale.</para>
    /// </summary>
    public CoverageFallbackContext Con(IReadOnlySet<string> online) =>
        new(_settori, online, _punti, _dichiarate, _padreDi);

    /// <summary>I callsign di tutti i settori che hanno un volume: l'insieme «tutti aperti».</summary>
    public IReadOnlySet<string> TuttiISettori =>
        new HashSet<string>(_perCallsign.Keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>Le righe dichiarate, per chi deve camminare la catena accanto a questo contesto.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> Dichiarate => _dichiarate;

    /// <summary>Il padre effettivo, per lo stesso motivo.</summary>
    public Func<string, string?> PadreDi => _padreDi;

    /// <summary>Chi raccoglie il traffico di quel punto se <paramref name="riceventeNominale"/> è chiuso.</summary>
    public CoverageFallbackResult Risolvi(string? cop, int? levelFeet, string? cedente, string? riceventeNominale)
    {
        if (levelFeet is not int ft) return CoverageFallbackResult.No(CoverageFallbackOutcome.NoLevel);

        var ricevente = riceventeNominale is null ? null : _perCallsign.GetValueOrDefault(riceventeNominale);

        return CoverageFallback.Resolve(
            cop, ft, _punti, Claims(ft),
            // ⚠️ Ricevente sconosciuto ai volumi (un estero senza forma, un callsign scritto a mano): la
            // soglia più alta, cioè si guardano i soli enti d'area. Meglio una risposta prudente che una
            // sbagliata — e non è un caso di scuola, i settori senza forma sono 61.
            tipoRicevente: ricevente?.Type ?? SectorType.Ctr,
            accRicevente: ricevente?.AccCode,
            fuoriGioco: Dominio(cedente),
            accDi: cs => _perCallsign.GetValueOrDefault(cs)?.AccCode,
            riceventeCallsign: riceventeNominale);
    }

    /// <summary>Come lo vuole <see cref="FallbackChain.Candidates"/>.</summary>
    public Func<IReadOnlyList<string>> Per(string? cop, int? levelFeet, string? cedente, string? riceventeNominale) =>
        () => Risolvi(cop, levelFeet, cedente, riceventeNominale).AsCandidates();

    private IReadOnlyList<SectorClaim> Claims(int quotaFt)
    {
        if (_claimsPerQuota.TryGetValue(quotaFt, out var gia)) return gia;

        // ⚠️ `resolveCoverage` NON passato: dentro la risoluzione di un rinvio i rinvii non si consultano.
        var claims = SectorVolumeMap.BuildClaims(_settori, _online,
            cs => TransferOnlineResolver.FirstOnline(
                FallbackChain.Candidates(cs, quotaFt, _dichiarate, _padreDi), _online));

        _claimsPerQuota[quotaFt] = claims;
        return claims;
    }

    /// <summary>Il cedente e tutti i suoi discendenti nell'albero di copertura.</summary>
    private IReadOnlySet<string> Dominio(string? cedente)
    {
        if (string.IsNullOrWhiteSpace(cedente))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_dominii.TryGetValue(cedente, out var gia)) return gia;

        var dentro = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cedente };
        var cresciuto = true;
        while (cresciuto)
        {
            cresciuto = false;
            foreach (var s in _settori)
                if (s.ParentCallsign is { Length: > 0 } p && dentro.Contains(p) && dentro.Add(s.Callsign))
                    cresciuto = true;
        }

        _dominii[cedente] = dentro;
        return dentro;
    }
}
