using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>Uno scalo trovato dalla ricerca della vista live: quanto basta per aprirgli il pannello rapido.</summary>
/// <param name="AccCode">L'ACC del documento: il pannello lo usa per gli indirizzi, e lo scalo cercato può stare in
/// un ACC diverso da quello di chi guarda (sono la NE di Roma e cerco LIPE).</param>
public sealed record LiveAeroportoTrovato(string Icao, string Nome, string AccCode, bool HaVipi, bool HaVsop);

/// <summary>
/// La RICERCA RAPIDA della vista live (committente, 1 ottobre 2026): da una postazione che ha scali sotto (APP, ACC)
/// si cerca uno scalo qualunque con vIPI o vSOP pubblicati — e lo si apre come se fosse del proprio settore — o le
/// aree regolamentate, tutte, da vedere su mappa con le informazioni di attivazione.
/// <para>Gli elenchi sono piccoli (qualche centinaio di righe) e si chiedono UNA volta, all'apertura del pannello: il
/// filtro gira poi in memoria a ogni tasto (<see cref="RicercaLiveFiltro"/>), senza una query per lettera.</para>
/// </summary>
public interface IRicercaLive
{
    /// <summary>Gli scali con vIPI civile o vSOP militare PUBBLICI (lo stesso cancello del quadro vAWOS), con il loro ACC.</summary>
    Task<IReadOnlyList<LiveAeroportoTrovato>> AeroportiAsync(CancellationToken ct = default);

    /// <summary>Tutte le aree regolamentate dell'anagrafica.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default);

    /// <summary>Le aree scelte, con poligono e testo di attivazione, nell'ordine degli id.</summary>
    Task<IReadOnlyList<AccSpecialAreaView>> DettagliAreeAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default);
}

internal sealed class RicercaLive(IDocumentAdminService documenti, ISpecialAreaRepository aree) : IRicercaLive
{
    public async Task<IReadOnlyList<LiveAeroportoTrovato>> AeroportiAsync(CancellationToken ct = default)
    {
        var docs = await documenti.ListAsync(ct);
        var trovati = new List<LiveAeroportoTrovato>();
        foreach (var a in Awos.AwosGate.Elenco(docs))
        {
            // L'ACC dal documento dello scalo (civile prima, militare poi): senza, il pannello non saprebbe dove portare.
            var acc = docs
                .Where(m => m.Kind is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil
                            && string.Equals(m.Scope, a.Icao, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(m.AccCode))
                .OrderBy(m => m.Kind == ReleaseTargetType.Airport ? 0 : 1)
                .Select(m => m.AccCode)
                .FirstOrDefault();
            if (acc is null) continue;
            trovati.Add(new LiveAeroportoTrovato(a.Icao, a.Nome, acc, a.HaVipi, a.HaVsop));
        }
        return trovati;
    }

    public Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default) => aree.ListAllSpecialAreasAsync(ct);

    public async Task<IReadOnlyList<AccSpecialAreaView>> DettagliAreeAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default)
    {
        if (ivaoIds.Count == 0) return Array.Empty<AccSpecialAreaView>();
        var dettagli = await aree.GetSpecialAreasByIdsAsync(ivaoIds, ct);
        return SpecialAreaProjection.Build(dettagli, ivaoIds);
    }
}

/// <summary>
/// Il confronto della ricerca live, puro e provato da solo. Si confronta il testo RIPULITO — maiuscole, senza spazi né
/// trattini né punti — perché le aree si scrivono in dieci modi: «LI R14A - S.Severa», «LI-R14», «R14», «r 14 a»
/// devono trovare la stessa riga.
/// </summary>
public static class RicercaLiveFiltro
{
    /// <summary>Sotto questa lunghezza non si cerca: una lettera sola troverebbe mezza Italia.</summary>
    public const int MinimoCaratteri = 2;

    /// <summary>Quante aree al massimo si elencano: oltre, si chiede di restringere.</summary>
    public const int MassimoAree = 60;

    public static string Pulito(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var b = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) if (char.IsLetterOrDigit(c)) b.Append(char.ToUpperInvariant(c));
        return b.ToString();
    }

    /// <summary>Gli scali: prima l'ICAO esatto, poi quelli che cominciano così, poi il nome che lo contiene.</summary>
    public static IReadOnlyList<LiveAeroportoTrovato> Aeroporti(IEnumerable<LiveAeroportoTrovato> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length < MinimoCaratteri) return Array.Empty<LiveAeroportoTrovato>();
        return elenco
            .Select(a => (a, peso: a.Icao.Equals(p, StringComparison.OrdinalIgnoreCase) ? 0
                                 : a.Icao.StartsWith(p, StringComparison.OrdinalIgnoreCase) ? 1
                                 : Pulito(a.Nome).Contains(p, StringComparison.Ordinal) ? 2 : -1))
            .Where(x => x.peso >= 0)
            .OrderBy(x => x.peso).ThenBy(x => x.a.Icao, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.a)
            .ToList();
    }

    /// <summary>
    /// Le aree: il nome ripulito che contiene il testo ripulito, oppure il TIPO uguale al testo («TRA», «D»: tutte
    /// quelle di quel tipo). Ordinate per nome.
    /// </summary>
    public static IReadOnlyList<SpecialAreaPick> Aree(IEnumerable<SpecialAreaPick> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length == 0) return Array.Empty<SpecialAreaPick>();
        return elenco
            .Where(a => (p.Length >= MinimoCaratteri && Pulito(a.Name).Contains(p, StringComparison.Ordinal))
                        || string.Equals(Pulito(a.Type), p, StringComparison.Ordinal))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
