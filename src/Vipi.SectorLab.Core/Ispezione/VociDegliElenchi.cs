using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// Le voci degli editor a elenco (lotto «Subito», slice 3b): gli scali dell'<c>.ap</c>, le piste del <c>.rw</c>, le
/// posizioni dei <c>.frq</c>, le attese in rotta di <c>HOLDENR.hold</c> (slice 10a). Si leggono dai record della sessione come sono ADESSO (una pista aggiunta nel Lab si
/// propone subito), da tutti i file: le copie gemelle dei file di FIR dicono le stesse cose.
/// </summary>
public sealed partial class VociDegliElenchi
{
    private readonly Dictionary<string, List<string>> _piste;

    private VociDegliElenchi(
        IReadOnlyList<string> scali, IReadOnlyList<string> posizioni, IReadOnlyList<string> attese, Dictionary<string, List<string>> piste)
    {
        Scali = scali;
        Posizioni = posizioni;
        Attese = attese;
        _piste = piste;
    }

    /// <summary>I codici ICAO degli scali, in ordine alfabetico (non i commentati: Aurora non li legge).</summary>
    public IReadOnlyList<string> Scali { get; }

    /// <summary>I nominativi delle posizioni ATC, in ordine alfabetico.</summary>
    public IReadOnlyList<string> Posizioni { get; }

    /// <summary>I nomi delle attese in rotta di <c>[HOLDENR]</c>, in ordine alfabetico (slice 10a: il campo attesa dei NAVAIDS).</summary>
    public IReadOnlyList<string> Attese { get; }

    /// <summary>La voce del menu generale di Aurora: le mappe degli <c>.str</c> che non stanno su una pista.</summary>
    public const string Maps = "MAPS";

    /// <summary>
    /// Le piste del <c>.rw</c> di uno scalo coi due versi di ognuna, nell'ordine del file (16L, 34R, 07, 25…), e in
    /// coda <see cref="Maps"/>. 🔴 Le voci di menu del <c>.rw</c> (<c>//MENU MAPPE</c>, <c>//ACC</c>) per il motore non
    /// sono record — sono righe che tiene com'erano — e quindi da qui non si vedono: <c>MAPS</c> si propone sempre,
    /// perché ogni scalo ce l'ha (96 sul fork); le voci di settore (<c>LIRR;NE</c>) sono della slice 11.
    /// </summary>
    public IReadOnlyList<string> PisteDi(string? scalo)
        => scalo is not { Length: > 0 } ? []
            : _piste.TryGetValue(scalo.Trim(), out var sue) ? [.. sue, Maps]
            : [Maps];

    /// <summary>Vero per una pista vera (01-36, con L/C/R), falso per una voce di menu del <c>.rw</c>.</summary>
    public static bool EUnaPistaVera(string voce) => PistaVera().IsMatch(voce.Trim());

    /// <summary>Le voci per quella fonte; per le piste serve lo scalo del record.</summary>
    public IReadOnlyList<string> Voci(FonteDellElenco fonte, string? scalo = null) => fonte switch
    {
        FonteDellElenco.Scali => Scali,
        FonteDellElenco.Posizioni => Posizioni,
        FonteDellElenco.Attese => Attese,
        FonteDellElenco.Piste => PisteDi(scalo),
        _ => [],
    };

    public static VociDegliElenchi Di(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        var scali = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var posizioni = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var attese = new SortedSet<string>(StringComparer.Ordinal);
        var piste = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (file is not IFileConRecord conRecord)
                continue;
            foreach (object record in conRecord.RecordDelModello)
            {
                switch (record)
                {
                    case AirportInfo { IsDisabled: false } scalo when scalo.IcaoCode.Length > 0:
                        scali.Add(scalo.IcaoCode.Trim());
                        break;
                    case AtcPosition posizione when posizione.Code.Length > 0:
                        posizioni.Add(posizione.Code.Trim());
                        break;
                    case Attesa attesa when attesa.Nome.Trim().Length > 0:
                        attese.Add(attesa.Nome.Trim());
                        break;
                    case Runway pista when pista.IcaoCode.Length > 0:
                        if (!piste.TryGetValue(pista.IcaoCode.Trim(), out var sue))
                            piste[pista.IcaoCode.Trim()] = sue = [];
                        foreach (string verso in new[] { pista.Designator1, pista.Designator2 }.Select(v => v.Trim()))
                        {
                            if (verso.Length > 0 && !sue.Contains(verso, StringComparer.OrdinalIgnoreCase))
                                sue.Add(verso);
                        }

                        break;
                }
            }
        }

        return new VociDegliElenchi([.. scali], [.. posizioni], [.. attese], piste);
    }

    [GeneratedRegex(@"^(0[1-9]|[12][0-9]|3[0-6])[LCR]?$")]
    private static partial Regex PistaVera();
}
