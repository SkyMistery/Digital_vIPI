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
/// <param name="Mappa">Il record della mappa: uno <see cref="StrRecord"/>, o una <see cref="SidProcedure"/> (la mappa di gruppo
/// in testa al <c>.sid</c>, P10, carta F7 slice 7f).</param>
public sealed record MappaComposta(object Mappa, string Valore, IReadOnlyList<ProceduraDellaComposta>? Elenco, bool Intere, int Riga)
{
    /// <summary>La mappa rigenerata dalle procedure del file (vuota se l'elenco non si legge).</summary>
    public MappaRigenerata Componi(IReadOnlyList<object> recordDelFile)
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
/// <para><b>Le SID</b> (P10, carta F7 slice 7f): una voce <c>MAPS</c> del <c>.sid</c> si compone allo stesso modo dalle SID
/// col tracciato dello stesso file. Nel <c>.sid</c> un tratto nuovo comincia dopo una riga vuota (non col
/// <c>&lt;br&gt;</c>), e la testa non si ripete: il primo punto porta l'etichetta, la coda del nome della SID
/// (<c>GEMMA;GEMMA;7G;</c> per <c>ALAX7G</c>), se il tracciato non ne ha già una.</para>
/// </remarks>
public static class MappeComposte
{
    /// <summary>Le mappe composte di un <c>.str</c> letto: i record <c>MAPS</c> che hanno la chiave <c>composta</c>.</summary>
    public static IReadOnlyList<MappaComposta> Di(ParseResult<StrRecord> letto)
    {
        ArgumentNullException.ThrowIfNull(letto);
        return Metadati.Leggi(letto, Metadati.NomeStr).Record
            .Where(m => m.Record.RunwaySpec == "MAPS" && m.Chiavi.ContainsKey(Metadati.Compose))
            .Select(m => new MappaComposta(m.Record, m.Chiavi[Metadati.Compose], Metadati.ElencoDellaComposta(m.Chiavi[Metadati.Compose]),
                m.Chiavi.GetValueOrDefault(Metadati.Whole) == "si", m.Riga))
            .ToList();
    }

    /// <summary>Le mappe composte di un <c>.sid</c> letto: le voci <c>MAPS</c> che hanno la chiave <c>composta</c> (P10).</summary>
    public static IReadOnlyList<MappaComposta> Di(ParseResult<SidProcedure> letto)
    {
        ArgumentNullException.ThrowIfNull(letto);
        return Metadati.Leggi(letto, Metadati.NomeSid).Record
            .Where(m => EUnaMappa(m.Record) && m.Chiavi.ContainsKey(Metadati.Compose))
            .Select(m => new MappaComposta(m.Record, m.Chiavi[Metadati.Compose], Metadati.ElencoDellaComposta(m.Chiavi[Metadati.Compose]),
                m.Chiavi.GetValueOrDefault(Metadati.Whole) == "si", m.Riga))
            .ToList();
    }

    /// <summary>Le mappe composte di un file letto, <c>.str</c> o <c>.sid</c>; nessuna per gli altri.</summary>
    public static IReadOnlyList<MappaComposta> DiQualunque(object letto) => letto switch
    {
        ParseResult<StrRecord> str => Di(str),
        ParseResult<SidProcedure> sid => Di(sid),
        _ => [],
    };

    /// <summary>Il nome di una procedura o di una mappa (<c>ODIN4E</c>, <c>ALAX7G</c>), senza spazi in testa e in coda.</summary>
    public static string Nome(object record) => record switch
    {
        StrRecord str => str.ProcedureId.Trim(),
        SidProcedure sid => sid.Name.Trim(),
        _ => string.Empty,
    };

    /// <summary>Il campo delle piste: <c>16L:16R</c>, <c>MAPS</c>.</summary>
    public static string Piste(object record) => record switch
    {
        StrRecord str => str.RunwaySpec,
        SidProcedure sid => sid.Runway,
        _ => string.Empty,
    };

    /// <summary>Vero per una voce del menu delle mappe (<c>MAPS</c>), in un <c>.str</c> o in un <c>.sid</c>.</summary>
    public static bool EUnaMappa(object record) => Piste(record).Trim() == "MAPS";

    /// <summary>Le procedure del file che un elenco nomina, nell'ordine dell'elenco (e del file, per lo stesso nome).</summary>
    public static IReadOnlyList<object> ProcedureElencate(IReadOnlyList<ProceduraDellaComposta> elenco, IEnumerable<object> recordDelFile)
    {
        ArgumentNullException.ThrowIfNull(elenco);
        var procedure = recordDelFile.Where(r => !EUnaMappa(r)).ToList();
        return elenco.SelectMany(voce => procedure.Where(p => Nomina(voce, p))).ToList();
    }

    /// <summary>
    /// La mappa come verrebbe rigenerata dalle procedure elencate (D8), coi tratti liberi di oggi in fondo (D9).
    /// Non tocca niente.
    /// </summary>
    public static MappaRigenerata Componi(object mappa, IReadOnlyList<ProceduraDellaComposta> elenco, IReadOnlyList<object> recordDelFile,
                                          bool intere = false)
    {
        ArgumentNullException.ThrowIfNull(mappa);
        ArgumentNullException.ThrowIfNull(elenco);
        ArgumentNullException.ThrowIfNull(recordDelFile);
        bool sid = mappa is SidProcedure;
        var procedure = recordDelFile.Where(r => !EUnaMappa(r)).ToList();

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
                var suoi = PuntiDellaParte(procedura);
                if (suoi.Count == 0)
                {
                    continue;
                }

                // Nel .str la testa si ripete (la riga col <br>, poi quella col suffisso); nel .sid il tratto comincia dal
                // primo punto, che porta l'etichetta, dopo una riga vuota.
                var tratto = sid ? [] : new List<PuntoDellaMappa> { suoi[0] with { Suffisso = null, IniziaUnTratto = true } };
                foreach (var punto in suoi)
                {
                    bool primo = tratto.Count == (sid ? 0 : 1);
                    tratto.Add(punto with { IniziaUnTratto = sid && tratto.Count == 0 });
                    // D8: si ferma sul primo punto già disegnato, compreso. Una SID no sul suo primo punto: le SID di una
                    // pista partono spesso dallo stesso punto, e la seconda si disegna fino a dove torna sulla prima.
                    if (!intere && disegnati.Contains(punto.Chiave) && !(sid && primo))
                    {
                        break;
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
        if (sid)
        {
            // Il primo punto del corpo segue la testata: nessuna riga vuota prima.
            if (punti.Count > 0)
            {
                punti[0] = punti[0] with { IniziaUnTratto = false };
            }
        }
        else if (punti.Count > 0 && PuntiDi(mappa) is { Count: > 0 } oggi)
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

    /// <summary>I punti del corpo di un record, qualunque sia il suo tipo (anche il tracciato di una SID).</summary>
    public static IReadOnlyList<PuntoDellaMappa> PuntiDi(object record) => record switch
    {
        SidProcedure s => s.Track.Select(t => new PuntoDellaMappa(t.Punto.Nome, t.Punto.NomeLongitudine, t.Punto.Posizione, t.Etichetta, t.NuovoTratto)).ToList(),
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

    /// <summary>
    /// I punti di una procedura come parte di una mappa: quelli del suo corpo; per una SID il primo porta la coda del nome
    /// come etichetta (<c>ALAX7G</c> → <c>7G</c>), se non ne ha già una: è l'etichetta che una mappa di gruppo mostra.
    /// </summary>
    public static IReadOnlyList<PuntoDellaMappa> PuntiDellaParte(object procedura)
    {
        var punti = PuntiDi(procedura);
        if (procedura is SidProcedure sid && punti.Count > 0 && punti[0].Suffisso is null
            && System.Text.RegularExpressions.Regex.Match(sid.Name.Trim().Split('-')[0], @"\d+[A-Z]$") is { Success: true } coda)
        {
            return [punti[0] with { Suffisso = coda.Value }, .. punti.Skip(1)];
        }

        return punti;
    }

    /// <summary>Vero se la mappa ha già esattamente quei punti.</summary>
    public static bool Uguale(object mappa, IReadOnlyList<PuntoDellaMappa> punti)
        => PuntiDi(mappa).SequenceEqual(punti);

    /// <summary>
    /// Mette quei punti nel corpo della mappa, in posto. Falso (e la mappa non cambia) se il suo tipo non li può
    /// tenere: una mappa di soli nomi non prende una coordinata, una di sole coordinate non prende un nome.
    /// </summary>
    public static bool Applica(object mappa, IReadOnlyList<PuntoDellaMappa> punti)
    {
        ArgumentNullException.ThrowIfNull(mappa);
        ArgumentNullException.ThrowIfNull(punti);
        switch (mappa)
        {
            case SidProcedure s:
                s.Track.Clear();
                foreach (var p in punti)
                {
                    s.Track.Add(new PuntoDelTracciato
                    {
                        Punto = p.Nome is null ? Punto.Da(p.Posizione!.Value) : Punto.Nominato(p.Nome, p.Etichetta ?? p.Nome),
                        Etichetta = p.Suffisso,
                        NuovoTratto = p.IniziaUnTratto,
                    });
                }

                return true;

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
    public static IReadOnlyList<ProceduraDellaComposta> ProcedureCheDisegna(object mappa, IReadOnlyList<object> recordDelFile)
    {
        ArgumentNullException.ThrowIfNull(mappa);
        ArgumentNullException.ThrowIfNull(recordDelFile);
        var procedure = recordDelFile.Where(r => !EUnaMappa(r)).ToList();
        var elenco = new List<ProceduraDellaComposta>();
        foreach (var tratto in Tratti(PuntiDi(mappa)))
        {
            if (DiQualeProcedura(tratto, procedure) is not { } procedura || !Metadati.NomeElencabile(Nome(procedura)))
            {
                continue;
            }

            string nome = Nome(procedura);
            bool unico = procedure.Count(p => Nome(p) == nome) == 1;
            var voce = new ProceduraDellaComposta(unico ? null : Piste(procedura).Split(':')[0].Trim(), nome);
            if (!elenco.Contains(voce))
            {
                elenco.Add(voce);
            }
        }

        return elenco;
    }

    /// <summary>Vero se la voce dell'elenco nomina quella procedura: stesso nome, e la pista se la voce la sceglie.</summary>
    public static bool Nomina(ProceduraDellaComposta voce, object procedura)
        => Nome(procedura) == voce.Nome
           && (voce.Pista is null || Piste(procedura).Split(':').Select(p => p.Trim()).Contains(voce.Pista));

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
    private static bool EDiUnaProcedura(List<PuntoDellaMappa> tratto, IReadOnlyList<object> procedure)
        => DiQualeProcedura(tratto, procedure) is not null;

    private static object? DiQualeProcedura(List<PuntoDellaMappa> tratto, IReadOnlyList<object> procedure)
    {
        var chiavi = tratto.Select(p => p.Chiave).ToList();
        if (chiavi.Count >= 2 && chiavi[0] == chiavi[1])
        {
            chiavi.RemoveAt(0);   // il primo punto ripetuto: la riga col <br> e quella col suffisso
        }

        string? suffisso = tratto.Select(p => p.Suffisso).FirstOrDefault(s => s is not null);
        return procedure.FirstOrDefault(procedura =>
        {
            var suoi = PuntiDellaParte(procedura);
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
