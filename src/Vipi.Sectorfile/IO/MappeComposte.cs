using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>Un punto del corpo di un record <c>.str</c>, qualunque sia il suo tipo: per nome o per coordinate.</summary>
/// <param name="Nome">Il nome del punto (primo campo), o null per un punto per coordinate.</param>
/// <param name="Etichetta">Il secondo campo di un punto per nome (di solito uguale al nome).</param>
/// <param name="Suffisso">Il terzo campo che fa da etichetta (<c>4E</c> di <c>ODINA;ODINA;4E;</c>).</param>
public sealed record PuntoDellaMappa(string? Nome, string? Etichetta, Coordinate? Posizione, string? Suffisso, bool IniziaUnTratto)
{
    /// <summary>Il punto come si riconosce fra i tratti: il nome, o la coordinata scritta.</summary>
    public string Chiave => Nome ?? $"{CoordinateConverter.LatitudeToDottedDms(Posizione!.Value.LatitudeDeg)};"
                                    + CoordinateConverter.LongitudeToDottedDms(Posizione!.Value.LongitudeDeg);
}

/// <summary>
/// Una mappa composta del file: il record <c>MAPS</c>, il valore di <c>composta</c>, l'elenco letto (null se non si
/// legge), se le procedure si disegnano intere (<c>intere=si</c>) e la riga della dichiarazione.
/// </summary>
public sealed record MappaComposta(StrRecord Mappa, string Valore, IReadOnlyList<ProceduraDellaComposta>? Elenco, bool Intere, int Riga)
{
    /// <summary>La mappa rigenerata dalle procedure del file (vuota se l'elenco non si legge).</summary>
    public MappaRigenerata Componi(IReadOnlyList<StrRecord> recordDelFile)
        => Elenco is null
            ? new MappaRigenerata([], [], 0)
            : MappeComposte.Componi(Mappa, Elenco, recordDelFile, Intere);
}

/// <summary>Come verrebbe la mappa rigenerata, e le procedure dell'elenco che nel file non ci sono.</summary>
public sealed record MappaRigenerata(IReadOnlyList<PuntoDellaMappa> Punti, IReadOnlyList<ProceduraDellaComposta> Mancanti, int TrattiLiberi);

/// <summary>
/// Le mappe composte (carta F3-bis §2.2, slice 4): una mappa <c>MAPS</c> di un <c>.str</c> che dichiara
/// <c>composta=ODIN4E,25:NENI5A</c> si rigenera dalle procedure elencate, dello stesso file.
/// </summary>
/// <remarks>
/// <para>Com'è fatta, misurato nella slice 0 sui 58 aggregati del fork: una procedura per tratto, nell'ordine
/// dell'elenco. Il tratto comincia con il primo punto col <c>&lt;br&gt;</c> (<c>ODINA;ODINA;&lt;br&gt;</c>), poi lo
/// stesso punto col suffisso che fa da etichetta (<c>ODINA;ODINA;4E;</c>), poi gli altri punti della procedura.</para>
/// <para><b>D8</b> (rivista il 23 settembre): una procedura che si innesta su un tratto già disegnato si ferma sul
/// primo punto già disegnato, compreso (così 102 dei 139 tratti troncati di oggi), a meno che la mappa dica
/// <c>intere=si</c>: 7 mappe del fork (<c>lirs</c>, <c>libp</c>…) disegnano intere anche le procedure che si toccano. <b>D9</b>: i tratti della mappa che non sono il disegno di una
/// procedura del file (un arco a coordinate, un tratto a mano) restano, in fondo, com'erano.</para>
/// </remarks>
public static class MappeComposte
{
    /// <summary>Le mappe composte di un <c>.str</c> letto: i record <c>MAPS</c> che hanno la chiave <c>composta</c>.</summary>
    public static IReadOnlyList<MappaComposta> Di(ParseResult<StrRecord> letto)
    {
        ArgumentNullException.ThrowIfNull(letto);
        return Metadati.Leggi(letto, Metadati.NomeStr).Record
            .Where(m => m.Record.RunwaySpec == "MAPS" && m.Chiavi.ContainsKey("composta"))
            .Select(m => new MappaComposta(m.Record, m.Chiavi["composta"], Metadati.ElencoDellaComposta(m.Chiavi["composta"]),
                m.Chiavi.GetValueOrDefault("intere") == "si", m.Riga))
            .ToList();
    }

    /// <summary>Le procedure del file che un elenco nomina, nell'ordine dell'elenco (e del file, per lo stesso nome).</summary>
    public static IReadOnlyList<StrRecord> ProcedureElencate(IReadOnlyList<ProceduraDellaComposta> elenco, IEnumerable<StrRecord> recordDelFile)
    {
        ArgumentNullException.ThrowIfNull(elenco);
        var procedure = recordDelFile.Where(r => r.RunwaySpec != "MAPS").ToList();
        return elenco.SelectMany(voce => procedure.Where(p => Nomina(voce, p))).ToList();
    }

    /// <summary>
    /// La mappa come verrebbe rigenerata dalle procedure elencate (D8), coi tratti liberi di oggi in fondo (D9).
    /// Non tocca niente.
    /// </summary>
    public static MappaRigenerata Componi(StrRecord mappa, IReadOnlyList<ProceduraDellaComposta> elenco, IReadOnlyList<StrRecord> recordDelFile,
                                          bool intere = false)
    {
        ArgumentNullException.ThrowIfNull(mappa);
        ArgumentNullException.ThrowIfNull(elenco);
        ArgumentNullException.ThrowIfNull(recordDelFile);
        var procedure = recordDelFile.Where(r => r.RunwaySpec != "MAPS").ToList();

        var punti = new List<PuntoDellaMappa>();
        var disegnati = new HashSet<string>(StringComparer.Ordinal);
        var mancanti = new List<ProceduraDellaComposta>();
        foreach (var voce in elenco)
        {
            var sue = procedure.Where(p => Nomina(voce, p)).ToList();
            if (sue.Count == 0)
            {
                mancanti.Add(voce);
                continue;
            }

            foreach (var procedura in sue)
            {
                var suoi = PuntiDi(procedura);
                if (suoi.Count == 0)
                {
                    continue;
                }

                var tratto = new List<PuntoDellaMappa> { suoi[0] with { Suffisso = null, IniziaUnTratto = true } };
                foreach (var punto in suoi)
                {
                    tratto.Add(punto with { IniziaUnTratto = false });
                    if (!intere && disegnati.Contains(punto.Chiave))
                    {
                        break;   // D8: si ferma sul primo punto già disegnato, compreso.
                    }
                }

                disegnati.UnionWith(tratto.Select(p => p.Chiave));
                punti.AddRange(tratto);
            }
        }

        // D9: i tratti di oggi che non sono il disegno di nessuna procedura del file restano, in fondo, com'erano.
        var liberi = Tratti(PuntiDi(mappa)).Where(t => !EDiUnaProcedura(t, procedure)).ToList();
        foreach (var tratto in liberi)
        {
            punti.AddRange(tratto.Select((p, i) => i == 0 ? p with { IniziaUnTratto = true } : p));
        }

        // Il primo tratto comincia comunque, e i file scrivono la sua testa in tre modi: `P;P;<br>` + `P;P;4E;` (lime.str),
        // `P;P;` + `P;P;3Z;` (liea.str), o solo `P;P;1J;` (limj.str). Si tiene la testa che la mappa ha oggi: una mappa
        // rigenerata uguale a quella di oggi non cambia nemmeno lì.
        if (punti.Count > 0 && PuntiDi(mappa) is { Count: > 0 } oggi)
        {
            bool testaDoppiaOggi = oggi.Count >= 2 && oggi[0].Chiave == oggi[1].Chiave;
            if (!testaDoppiaOggi && punti.Count >= 2 && punti[0].Suffisso is null && punti[0].Chiave == punti[1].Chiave)
            {
                punti.RemoveAt(0);
            }

            punti[0] = punti[0] with { IniziaUnTratto = oggi[0].IniziaUnTratto };
        }

        return new MappaRigenerata(punti, mancanti, liberi.Count);
    }

    /// <summary>I punti del corpo di un record, qualunque sia il suo tipo.</summary>
    public static IReadOnlyList<PuntoDellaMappa> PuntiDi(StrRecord record) => record switch
    {
        ProcedureStrRecord p => p.Waypoints.Select(w => new PuntoDellaMappa(w.FixName, w.DisplayLabel, null, w.SuffixCode, w.IniziaUnTratto)).ToList(),
        HoldingStrRecord h => h.Points.Select(p => p switch
        {
            HoldingFixPoint f => new PuntoDellaMappa(f.FixName, f.DisplayLabel, null, f.SuffixCode, f.IniziaUnTratto),
            HoldingCoordPoint c => new PuntoDellaMappa(null, null, c.Position, c.SuffixCode, c.IniziaUnTratto),
            _ => throw new InvalidOperationException("Punto sconosciuto."),
        }).ToList(),
        GeometricStrRecord g => g.Segments.SelectMany((s, i) => s.Points.Select((c, k) =>
            new PuntoDellaMappa(null, null, c, null, i > 0 && k == 0))).ToList(),
        _ => [],
    };

    /// <summary>Vero se la mappa ha già esattamente quei punti.</summary>
    public static bool Uguale(StrRecord mappa, IReadOnlyList<PuntoDellaMappa> punti)
        => PuntiDi(mappa).SequenceEqual(punti);

    /// <summary>
    /// Mette quei punti nel corpo della mappa, in posto. Falso (e la mappa non cambia) se il suo tipo non li può
    /// tenere: una mappa di soli nomi non prende una coordinata, una di sole coordinate non prende un nome.
    /// </summary>
    public static bool Applica(StrRecord mappa, IReadOnlyList<PuntoDellaMappa> punti)
    {
        ArgumentNullException.ThrowIfNull(mappa);
        ArgumentNullException.ThrowIfNull(punti);
        switch (mappa)
        {
            case HoldingStrRecord h:
                h.Points.Clear();
                foreach (var p in punti)
                {
                    h.Points.Add(p.Nome is null
                        ? new HoldingCoordPoint { Position = p.Posizione!.Value, SuffixCode = p.Suffisso, IniziaUnTratto = p.IniziaUnTratto }
                        : new HoldingFixPoint { FixName = p.Nome, DisplayLabel = p.Etichetta ?? p.Nome, SuffixCode = p.Suffisso, IniziaUnTratto = p.IniziaUnTratto });
                }

                return true;

            case ProcedureStrRecord w when punti.All(p => p.Nome is not null):
                w.Waypoints.Clear();
                foreach (var p in punti)
                {
                    w.Waypoints.Add(new ProcedureWaypoint { FixName = p.Nome!, DisplayLabel = p.Etichetta ?? p.Nome!, SuffixCode = p.Suffisso, IniziaUnTratto = p.IniziaUnTratto });
                }

                return true;

            case GeometricStrRecord g when punti.All(p => p.Nome is null):
                g.Segments.Clear();
                foreach (var p in punti)
                {
                    if (g.Segments.Count == 0 || p.IniziaUnTratto)
                    {
                        g.Segments.Add(new GeometricSegment());
                    }

                    g.Segments[^1].Points.Add(p.Posizione!.Value);
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Le procedure che la mappa disegna oggi, nell'ordine dei suoi tratti: l'elenco con cui un aggregato fatto a mano
    /// diventa composto senza cambiare (F3-bis slice 5). Il nome da solo se nel file è unico, se no con la sua prima
    /// pista (<c>25:NENI5A</c>). I tratti che non sono una procedura del file restano fuori: sono quelli liberi (D9); e
    /// così quelli di una procedura il cui nome non può stare nell'elenco (<see cref="Metadati.NomeElencabile"/>).
    /// </summary>
    public static IReadOnlyList<ProceduraDellaComposta> ProcedureCheDisegna(StrRecord mappa, IReadOnlyList<StrRecord> recordDelFile)
    {
        ArgumentNullException.ThrowIfNull(mappa);
        ArgumentNullException.ThrowIfNull(recordDelFile);
        var procedure = recordDelFile.Where(r => r.RunwaySpec != "MAPS").ToList();
        var elenco = new List<ProceduraDellaComposta>();
        foreach (var tratto in Tratti(PuntiDi(mappa)))
        {
            if (DiQualeProcedura(tratto, procedure) is not { } procedura || !Metadati.NomeElencabile(procedura.ProcedureId.Trim()))
            {
                continue;
            }

            string nome = procedura.ProcedureId.Trim();
            bool unico = procedure.Count(p => p.ProcedureId.Trim() == nome) == 1;
            var voce = new ProceduraDellaComposta(unico ? null : procedura.RunwaySpec.Split(':')[0].Trim(), nome);
            if (!elenco.Contains(voce))
            {
                elenco.Add(voce);
            }
        }

        return elenco;
    }

    /// <summary>Vero se la voce dell'elenco nomina quella procedura: stesso nome, e la pista se la voce la sceglie.</summary>
    public static bool Nomina(ProceduraDellaComposta voce, StrRecord procedura)
        => procedura.ProcedureId.Trim() == voce.Nome
           && (voce.Pista is null || procedura.RunwaySpec.Split(':').Select(p => p.Trim()).Contains(voce.Pista));

    /// <summary>I tratti di un corpo: si comincia a ogni punto col <c>&lt;br&gt;</c>.</summary>
    private static List<List<PuntoDellaMappa>> Tratti(IReadOnlyList<PuntoDellaMappa> punti)
    {
        var tratti = new List<List<PuntoDellaMappa>>();
        foreach (var punto in punti)
        {
            if (punto.IniziaUnTratto || tratti.Count == 0)
            {
                tratti.Add([]);
            }

            tratti[^1].Add(punto);
        }

        return tratti;
    }

    /// <summary>
    /// Un tratto è il disegno di una procedura del file se comincia dal suo primo punto e ha il suo suffisso
    /// (<c>ODINA</c> + <c>4E</c> = <c>ODIN4E</c>); senza suffisso, se i suoi punti sono l'inizio di quelli della procedura.
    /// </summary>
    private static bool EDiUnaProcedura(List<PuntoDellaMappa> tratto, IReadOnlyList<StrRecord> procedure)
        => DiQualeProcedura(tratto, procedure) is not null;

    private static StrRecord? DiQualeProcedura(List<PuntoDellaMappa> tratto, IReadOnlyList<StrRecord> procedure)
    {
        var chiavi = tratto.Select(p => p.Chiave).ToList();
        if (chiavi.Count >= 2 && chiavi[0] == chiavi[1])
        {
            chiavi.RemoveAt(0);   // il primo punto ripetuto: la riga col <br> e quella col suffisso
        }

        string? suffisso = tratto.Select(p => p.Suffisso).FirstOrDefault(s => s is not null);
        return procedure.FirstOrDefault(procedura =>
        {
            var suoi = PuntiDi(procedura);
            if (suoi.Count == 0 || suoi[0].Chiave != chiavi[0])
            {
                return false;
            }

            return suffisso is not null
                ? suoi[0].Suffisso == suffisso
                : suoi.Count >= chiavi.Count && suoi.Take(chiavi.Count).Select(p => p.Chiave).SequenceEqual(chiavi);
        });
    }
}
