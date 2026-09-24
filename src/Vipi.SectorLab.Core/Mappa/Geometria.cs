using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// Da record del motore a forme per la mappa (carta F3, slice 3). Non è un secondo modello del sector: è la
/// traduzione, un tipo per volta, di quel che il motore ha già letto.
/// <para>Le regole che contano, e che un disegno ingenuo sbaglia:</para>
/// <list type="bullet">
/// <item>un punto scritto PER NOME si risolve nel catalogo del master scelto; se non si risolve, il tratto lì si
/// interrompe e il nome si dice (è un errore del sector, non un buco da nascondere);</item>
/// <item>i <c>DUMMY</c> dei bordi <c>T;</c> e le righe vuote dei tracciati SID non sono rumore: **spezzano** il
/// tratto (carta F2, slice 4 e 5);</item>
/// <item>i segmenti dei <c>.geo</c> e delle aree P/R/D sono righe indipendenti: si cuciono in polilinee quando la
/// fine di uno è l'inizio del successivo (come la prova F0, 83 186 segmenti → 6 781 polilinee).</item>
/// </list>
/// </summary>
public static class Geometria
{
    /// <summary>Le forme di un file aperto, nell'ordine dei record. Vuoto per i file che il motore non interpreta.</summary>
    public static IReadOnlyList<FormaDellaMappa> DelFile(FileAperto file, CatalogoDeiPunti? catalogo)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord)
            return [];

        var forme = new List<FormaDellaMappa>();
        var record = conRecord.RecordDelModello;

        // I segmenti sciolti (.geo, aree P/R/D) si cuciono fra loro: la mappa vuole polilinee, non 83 000 segmenti.
        if (record.Count > 0 && record[0] is Line)
        {
            forme.AddRange(Segmenti(file.Relativo, record.Cast<Line>().ToList()));
            return forme;
        }

        for (int i = 0; i < record.Count; i++)
        {
            if (Forma(file.Relativo, i, record[i], catalogo) is { } forma)
                forme.Add(forma);
        }

        return forme;
    }

    private static FormaDellaMappa? Forma(string file, int indice, object record, CatalogoDeiPunti? catalogo)
    {
        var nonRisolti = new List<string>();

        switch (record)
        {
            // --- punti ------------------------------------------------------------------------------------------
            case Fix f:
                return Punto(file, indice, f.Name, f.Position);
            case Vor v:
                return Punto(file, indice, v.Ident, v.Position);
            case Ndb n:
                return Punto(file, indice, n.Ident, n.Position);
            case VfrPoint p:
                return Punto(file, indice, p.Name, p.Position);
            case AirportInfo a:
                return Punto(file, indice, a.IcaoCode, a.Centre);
            case Stand s:
                return Punto(file, indice, s.Number, s.Position);
            case TaxiwayLabel t:
                return Punto(file, indice, t.Name, t.Position);
            // Le posizioni ATC dei .frq non stanno su una mappa: sono frequenze e trasferimenti, senza coordinate.
            case Attesa attesa:
                return Risolvi(attesa.Posizione, catalogo, nonRisolti) is { } dove
                    ? Punto(file, indice, attesa.Nome, dove)
                    // Senza il fix non si disegna niente: una coordinata «vuota» sarebbe un punto nel golfo di Guinea.
                    : new FormaDellaMappa(file, indice, TipoDiForma.Punto, attesa.Nome.Trim(), [], nonRisolti);

            // --- linee ------------------------------------------------------------------------------------------
            case Runway pista:
                return new FormaDellaMappa(file, indice, TipoDiForma.Linea,
                    $"{pista.IcaoCode} {pista.Designator1}/{pista.Designator2}",
                    [[pista.Threshold1, pista.Threshold2]], []);

            case Airway aerovia:
                return Linea(file, indice, aerovia.Name,
                    aerovia.FixLabels.Select(l => Sectorfile.Shared.Punto.Nominato(l)), catalogo, nonRisolti);

            case RottaVfr rotta:
                return Linea(file, indice, "VFR " + rotta.Numero, rotta.Punti, catalogo, nonRisolti);

            case SidProcedure sid when sid.Track.Count > 0:
                return Tratti(file, indice, TipoDiForma.Linea, $"{sid.IcaoCode} {sid.Name}",
                    Spezza(sid.Track.Select(p => (p.Punto, p.NuovoTratto)), catalogo, nonRisolti), nonRisolti);

            // 🔴 Il <br> spezza la linea anche qui: le mappe che raccolgono procedure (STAR RNAV(ALL)…) sono per nome o
            // miste, e fino alla F3-bis il loro <br> si perdeva — ogni STAR si ricollegava alla successiva (committente,
            // prova 2 del 23 settembre, lirn.str STAR 06(ALL)). Dalla slice 3 il modello lo tiene: IniziaUnTratto.
            case ProcedureStrRecord procedura:
                return Tratti(file, indice, TipoDiForma.Linea, $"{procedura.IcaoCode} {procedura.ProcedureId}",
                    // I due campi sono i due nomi del punto: Aurora prende la latitudine dal primo e la longitudine
                    // dal secondo (F2 slice 4), e con un refuso in uno dei due il punto non si disegna.
                    Spezza(procedura.Waypoints.Select(w => (Sectorfile.Shared.Punto.Nominato(w.FixName, w.DisplayLabel), w.IniziaUnTratto)),
                        catalogo, nonRisolti), nonRisolti);

            case HoldingStrRecord attesa:
                return Tratti(file, indice, TipoDiForma.Linea, $"{attesa.IcaoCode} {attesa.ProcedureId}",
                    Spezza(attesa.Points.Select(p => p switch
                    {
                        HoldingFixPoint fisso => (Sectorfile.Shared.Punto.Nominato(fisso.FixName, fisso.DisplayLabel), fisso.IniziaUnTratto),
                        HoldingCoordPoint coordinata => (Sectorfile.Shared.Punto.Da(coordinata.Position), coordinata.IniziaUnTratto),
                        _ => (default(Sectorfile.Shared.Punto), false),
                    }), catalogo, nonRisolti), nonRisolti);

            // 🔴 Linea, non area: Aurora disegna le mappe degli .str punto dopo punto e non le chiude mai. Un'ATZ
            // col primo punto sbagliato in Aurora resta aperta; da noi Leaflet la chiudeva e l'errore non si vedeva
            // (committente, prova 10 del 23 settembre: lirn.str «LIRN ATZ»). Chiusa è solo se l'ultimo punto
            // ripete il primo — e così la disegna anche la linea.
            case GeometricStrRecord zona:
                return Tratti(file, indice, TipoDiForma.Linea, $"{zona.IcaoCode} {zona.ProcedureId}",
                    zona.Segments.Select(s => (IReadOnlyList<Coordinate>)s.Points.ToList())
                        .Where(s => s.Count > 0).ToList(), nonRisolti);

            // --- aree -------------------------------------------------------------------------------------------
            case Polygon poligono:
                return poligono.Vertices.Count == 0
                    ? null
                    : new FormaDellaMappa(file, indice, TipoDiForma.Area, poligono.FillColor,
                        [poligono.Vertices.ToList()], []);

            case TflSector settore:
                return Area(file, indice, settore.SectorCode, settore.Vertices, catalogo, nonRisolti);

            case MvaSector mva:
                return Area(file, indice, mva.AltLabel, mva.Vertices.Select(v => v.Position), catalogo, nonRisolti);

            case StaticBoundaryGroup gruppo:
                // Ogni poligono del gruppo è un tratto suo: i DUMMY che li separano li ha già divisi il lettore.
                return Tratti(file, indice, TipoDiForma.Linea, gruppo.Name,
                    gruppo.Polygons
                        .Select(p => (IReadOnlyList<Coordinate>)p.Vertices
                            .Select(v => v.Position ?? Risolvi(Vertice(v), catalogo, nonRisolti))
                            .Where(c => c is not null).Select(c => c!.Value).ToList())
                        .Where(t => t.Count > 0).ToList(),
                    nonRisolti);

            default:
                return null;
        }
    }

    private static Sectorfile.Shared.Punto Vertice(StaticBoundaryVertex vertice)
        => Sectorfile.Shared.Punto.Nominato(vertice.FixA ?? string.Empty, vertice.FixB);

    private static FormaDellaMappa Punto(string file, int indice, string etichetta, Coordinate posizione,
                                         IReadOnlyList<string>? nonRisolti = null)
        => new(file, indice, TipoDiForma.Punto, etichetta.Trim(), [[posizione]], nonRisolti ?? []);

    private static FormaDellaMappa? Linea(string file, int indice, string etichetta,
                                          IEnumerable<Sectorfile.Shared.Punto> punti,
                                          CatalogoDeiPunti? catalogo, List<string> nonRisolti)
        => Tratti(file, indice, TipoDiForma.Linea, etichetta,
                  Spezza(punti.Select(p => (p, false)), catalogo, nonRisolti), nonRisolti);

    private static FormaDellaMappa? Area(string file, int indice, string etichetta,
                                         IEnumerable<Sectorfile.Shared.Punto> punti,
                                         CatalogoDeiPunti? catalogo, List<string> nonRisolti)
        => Tratti(file, indice, TipoDiForma.Area, etichetta,
                  Spezza(punti.Select(p => (p, false)), catalogo, nonRisolti), nonRisolti);

    /// <summary>
    /// La forma, o nulla se non c'è niente da disegnare E niente da dire. 🔴 Un record che non si disegna PERCHÉ un
    /// nome non si risolve resta, con zero tratti e il nome mancante: senza questa riga spariva in silenzio, e con lui
    /// il motivo (trovato dalla misura sull'albero, slice 3b: `LIMN;35;IAF HITAC35` in `limn.str`).
    /// </summary>
    private static FormaDellaMappa? Tratti(string file, int indice, TipoDiForma tipo, string etichetta,
                                           IReadOnlyList<IReadOnlyList<Coordinate>> tratti, IReadOnlyList<string> nonRisolti)
    {
        var buoni = tratti.Where(t => t.Count > 0).ToList();
        return buoni.Count == 0 && nonRisolti.Count == 0
            ? null
            : new FormaDellaMappa(file, indice, tipo, etichetta.Trim(), buoni, nonRisolti);
    }

    /// <summary>
    /// I punti in tratti: un nome che non si risolve, o un punto che chiede un tratto nuovo, chiude quello aperto.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Coordinate>> Spezza(
        IEnumerable<(Sectorfile.Shared.Punto Punto, bool NuovoTratto)> punti,
        CatalogoDeiPunti? catalogo, List<string> nonRisolti)
    {
        var tratti = new List<IReadOnlyList<Coordinate>>();
        var corrente = new List<Coordinate>();

        void Chiudi()
        {
            if (corrente.Count > 0)
                tratti.Add(corrente.ToList());
            corrente.Clear();
        }

        foreach (var (punto, nuovo) in punti)
        {
            if (nuovo)
                Chiudi();

            if (Risolvi(punto, catalogo, nonRisolti) is { } posizione)
                corrente.Add(posizione);
            else
                Chiudi();
        }

        Chiudi();
        return tratti;
    }

    /// <summary>
    /// Le coordinate di un punto: le sue, o quelle del catalogo se è scritto per nome. La regola con DUE nomi diversi
    /// (latitudine dal primo, longitudine dal secondo, come Aurora) è del motore: <c>Punto.TryRisolvi</c>.
    /// </summary>
    private static Coordinate? Risolvi(Sectorfile.Shared.Punto punto, CatalogoDeiPunti? catalogo, List<string> nonRisolti)
    {
        if (punto.TryRisolvi(catalogo, out var posizione))
            return posizione;

        foreach (string? nome in new[] { punto.Nome, punto.NomeLongitudine })
        {
            if (nome is { Length: > 0 } && catalogo?.Risolve(nome) != true)
                nonRisolti.Add(nome);
        }

        return null;
    }

    /// <summary>
    /// I segmenti di un <c>.geo</c> (o di un'area P/R/D) cuciti in polilinee: la fine di un segmento è l'inizio del
    /// successivo finché il colore e il nome dell'area non cambiano. Una forma per polilinea, agganciata al PRIMO
    /// record che la compone.
    /// </summary>
    private static IEnumerable<FormaDellaMappa> Segmenti(string file, IReadOnlyList<Line> segmenti)
    {
        int primo = 0;
        var punti = new List<Coordinate>();
        string etichetta = string.Empty;

        for (int i = 0; i < segmenti.Count; i++)
        {
            var segmento = segmenti[i];
            string suo = segmento.Nome ?? segmento.Color;
            bool continua = punti.Count > 0
                            && string.Equals(suo, etichetta, StringComparison.Ordinal)
                            && Vicini(punti[^1], segmento.Start);

            if (!continua)
            {
                if (punti.Count > 1)
                    yield return new FormaDellaMappa(file, primo, TipoDiForma.Linea, etichetta, [punti.ToList()], []);
                punti.Clear();
                primo = i;
                etichetta = suo;
                punti.Add(segmento.Start);
            }

            punti.Add(segmento.End);
        }

        if (punti.Count > 1)
            yield return new FormaDellaMappa(file, primo, TipoDiForma.Linea, etichetta, [punti.ToList()], []);
    }

    /// <summary>Lo stesso punto scritto due volte: il sector arrotonda ai millesimi di secondo.</summary>
    private static bool Vicini(Coordinate a, Coordinate b)
        => Math.Abs(a.LatitudeDeg - b.LatitudeDeg) < 1e-7 && Math.Abs(a.LongitudeDeg - b.LongitudeDeg) < 1e-7;
}
