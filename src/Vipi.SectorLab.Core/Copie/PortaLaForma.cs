using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>Dove è andata la forma dopo un gesto: le copie che l'hanno ricevuta, e quelle no col perché.</summary>
public sealed record FormaPortataSulleCopie(IReadOnlyList<ParteDiForma> Portate, IReadOnlyList<(ParteDiForma Copia, string Perche)> NonPortate);

/// <summary>
/// La forma che si porta: il suo anello (senza il punto che lo chiude) e, per ogni vertice, com'era scritto — un nome
/// resta un nome dove la copia lo sa scrivere. Viene da un elenco di vertici o da una linea di un <c>.geo</c>.
/// </summary>
public sealed record FormaDiPartenza(IReadOnlyList<Coordinate> Anello, IReadOnlyDictionary<long, Punto> Punti);

/// <summary>
/// Portare una forma su una sua copia (lotto «Subito» slice 8c-8d, «file per file» D5, I2, H10): dopo un gesto la
/// stessa forma va sulle copie che erano uguali, e «allinea questa» / «prendi la sua» la portano su una copia diversa.
/// </summary>
/// <remarks>
/// La copia prende l'ANELLO della forma, non le sue righe: tiene il suo vertice di partenza, il suo verso, la sua
/// chiusura (il primo punto ripetuto in fondo, se c'era) e la scrittura dei vertici che restano — un punto per nome
/// resta per nome, una coordinata compatta resta compatta. Un vertice nuovo si scrive come lo scrive la forma di
/// partenza, se la copia lo sa scrivere (un <c>.pol</c> tiene solo coordinate: il nome diventa la sua posizione; i punti
/// di una procedura <c>.str</c> solo nomi: una coordinata non ci va, e la copia si rifiuta).
/// <para>Una linea di un <c>.geo</c> (8d) non è un elenco di vertici ma segmenti di fila: si riscrive come testo, un
/// segmento per lato; i segmenti che non cambiano restano le righe di prima, byte per byte.</para>
/// <para>Due vertici sono lo stesso al decimo di metro, come in <see cref="FormeUguali"/>.</para>
/// </remarks>
public static class PortaLaForma
{
    /// <summary>
    /// L'elenco dei vertici che disegna la parte <paramref name="parte"/> del record (la stessa numerazione delle forme
    /// della mappa): il poligono n-esimo di un confine, il tratto n-esimo non vuoto di una zona <c>.str</c>, l'unico
    /// elenco degli altri. Null dove la parte non è un elenco di vertici (le linee dei <c>.geo</c>).
    /// </summary>
    public static ElencoDiVertici? ElencoDellaParte(FileAperto file, int record, int parte)
    {
        ArgumentNullException.ThrowIfNull(file);
        var elenchi = ElenchiDiVertici.Di(file, record);
        if (elenchi.Any(e => e.Chiave.StartsWith("Polygons[", StringComparison.Ordinal)))
            return elenchi.FirstOrDefault(e => e.Chiave == $"Polygons[{parte}].Vertices");
        if (elenchi.Any(e => e.Chiave.StartsWith("Segments[", StringComparison.Ordinal)))
            return elenchi.Where(e => e.Chiave.StartsWith("Segments[", StringComparison.Ordinal) && e.Quanti > 0).ElementAtOrDefault(parte);
        return parte == 0 ? elenchi.FirstOrDefault(e => e.Chiave is "Vertices" or "Punti" or "Points" or "Waypoints") : null;
    }

    /// <summary>La forma di un elenco di vertici (i nomi risolti nel catalogo), o null se un nome non si risolve.</summary>
    public static FormaDiPartenza? Da(ElencoDiVertici elenco, IFixResolver? catalogo)
    {
        ArgumentNullException.ThrowIfNull(elenco);
        var punti = new List<Coordinate>(elenco.Quanti);
        var scritti = new Dictionary<long, Punto>();
        for (int i = 0; i < elenco.Quanti; i++)
        {
            var punto = elenco.PuntoDi(i);
            if (!punto.TryRisolvi(catalogo, out var posizione))
                return null;
            punti.Add(posizione);
            scritti.TryAdd(FormeUguali.Chiave(posizione), punto);
        }

        return new FormaDiPartenza(SenzaRipetuti(punti), scritti);
    }

    /// <summary>La forma di una linea di un <c>.geo</c>: i suoi punti, tutti per coordinate.</summary>
    public static FormaDiPartenza Da(LineaDelGeo linea)
    {
        ArgumentNullException.ThrowIfNull(linea);
        var scritti = new Dictionary<long, Punto>();
        foreach (var punto in linea.Punti)
            scritti.TryAdd(FormeUguali.Chiave(punto), Punto.Da(punto));
        return new FormaDiPartenza(SenzaRipetuti(linea.Punti), scritti);
    }

    /// <summary>Vero se le due forme sono lo stesso anello.</summary>
    public static bool Uguali(FormaDiPartenza a, FormaDiPartenza b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return FormeUguali.UgualiComeAnello([.. a.Anello.Select(FormeUguali.Chiave)], [.. b.Anello.Select(FormeUguali.Chiave)]);
    }

    /// <summary>
    /// Le voci nuove della copia: l'anello di <paramref name="da"/> scritto come lo scriverebbe <paramref name="copia"/>.
    /// Torna il motivo, se la copia non lo sa scrivere.
    /// </summary>
    public static IReadOnlyList<object>? VociPer(FormaDiPartenza da, ElencoDiVertici copia, IFixResolver? catalogo, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(da);
        ArgumentNullException.ThrowIfNull(copia);
        perche = null;
        if (da.Anello.Count < 3)
        {
            perche = "La forma di partenza ha meno di 3 vertici.";
            return null;
        }

        var vecchia = Da(copia, catalogo);
        var nuovo = Giro(da.Anello, vecchia?.Anello);

        // Le voci della copia per posizione, per tenerle com'erano scritte.
        var dellaCopia = new Dictionary<long, object>();
        for (int i = 0; i < copia.Quanti; i++)
        {
            if (copia.PuntoDi(i).TryRisolvi(catalogo, out var c))
                dellaCopia.TryAdd(FormeUguali.Chiave(c), copia.Elenco[i]!);
        }

        bool chiusa = copia.Quanti >= 3 && copia.PuntoDi(0).TryRisolvi(catalogo, out var inizio)
                      && copia.PuntoDi(copia.Quanti - 1).TryRisolvi(catalogo, out var fine)
                      && FormeUguali.Chiave(inizio) == FormeUguali.Chiave(fine);

        var voci = new List<object>(nuovo.Count + 1);
        foreach (var posizione in nuovo)
        {
            long chiave = FormeUguali.Chiave(posizione);
            if (dellaCopia.TryGetValue(chiave, out object? sua))
            {
                voci.Add(sua);
                continue;
            }

            if (Scrivibile(da.Punti.GetValueOrDefault(chiave, Punto.Da(posizione)), posizione, copia) is not { } punto)
            {
                perche = copia.AmmetteCoordinate
                    ? "Un vertice nuovo non si sa scrivere in questa copia."
                    : "Questa copia scrive i punti solo per nome, e un vertice nuovo ha solo le coordinate.";
                return null;
            }

            voci.Add(copia.Fabbrica(punto, vecchio: null));
        }

        // La chiusura: la voce che chiudeva, se ripete ancora la partenza; se no una voce nuova uguale alla prima (un
        // oggetto suo: due voci che sono lo stesso oggetto si cambierebbero insieme).
        if (chiusa)
        {
            long partenza = FormeUguali.Chiave(nuovo[0]);
            voci.Add(copia.PuntoDi(copia.Quanti - 1).TryRisolvi(catalogo, out var ultima) && FormeUguali.Chiave(ultima) == partenza
                ? copia.Elenco[copia.Quanti - 1]!
                : copia.Fabbrica(copia.PuntoDi(0).TryRisolvi(catalogo, out var prima) && FormeUguali.Chiave(prima) == partenza
                    ? copia.PuntoDi(0)
                    : Scrivibile(da.Punti.GetValueOrDefault(partenza, Punto.Da(nuovo[0])), nuovo[0], copia) ?? Punto.Da(nuovo[0]), vecchio: null));
        }

        return voci;
    }

    /// <summary>
    /// Le righe nuove di una linea di un <c>.geo</c> con la forma <paramref name="da"/> (8d): un segmento per lato, nel
    /// giro e con la partenza della linea, chiusa se lo era. Un segmento che c'era già resta la riga di prima; uno nuovo
    /// copia dalla prima riga della linea il tipo, l'area e quel che segue (senza un commento in coda: il Lab non ne
    /// scrive), con le coordinate nella forma di quella riga (col punto, o compatte).
    /// </summary>
    /// <param name="righe">Le righe dei segmenti della linea com'è adesso, una per segmento.</param>
    public static IReadOnlyList<string> RigheDellaLinea(FormaDiPartenza da, LineaDelGeo linea, IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(da);
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(righe);
        var vecchio = SenzaRipetuti(linea.Punti);
        bool chiusa = linea.Punti.Count > 3 && FormeUguali.Chiave(linea.Punti[0]) == FormeUguali.Chiave(linea.Punti[^1]);
        var giro = Giro(da.Anello, vecchio).ToList();
        if (chiusa)
            giro.Add(giro[0]);

        var diPrima = new Dictionary<(long, long), string>();
        for (int i = 0; i < righe.Count && i + 1 < linea.Punti.Count; i++)
            diPrima.TryAdd((FormeUguali.Chiave(linea.Punti[i]), FormeUguali.Chiave(linea.Punti[i + 1])), righe[i]);

        string modello = righe[0];
        int commento = modello.IndexOf("//", StringComparison.Ordinal);
        string[] campi = (commento >= 0 ? modello[..commento] : modello).TrimEnd().Split(';');
        bool compatte = Compatta.IsMatch(campi[0].Trim());

        var nuove = new List<string>(giro.Count);
        for (int i = 0; i + 1 < giro.Count; i++)
        {
            var (a, b) = (giro[i], giro[i + 1]);
            if (diPrima.TryGetValue((FormeUguali.Chiave(a), FormeUguali.Chiave(b)), out string? uguale))
            {
                nuove.Add(uguale);
                continue;
            }

            var riga = (string[])campi.Clone();
            (riga[0], riga[1], riga[2], riga[3]) = compatte
                ? (Compatto(a.LatitudeDeg, "NS", 3), Compatto(a.LongitudeDeg, "EW", 3), Compatto(b.LatitudeDeg, "NS", 3), Compatto(b.LongitudeDeg, "EW", 3))
                : (CoordinateConverter.LatitudeToDottedDms(a.LatitudeDeg), CoordinateConverter.LongitudeToDottedDms(a.LongitudeDeg),
                   CoordinateConverter.LatitudeToDottedDms(b.LatitudeDeg), CoordinateConverter.LongitudeToDottedDms(b.LongitudeDeg));
            nuove.Add(string.Join(";", riga));
        }

        return nuove;
    }

    // Un campo compatto: la lettera e almeno nove cifre (N0434857348).
    private static readonly Regex Compatta = new(@"^[NSEWnsew]\d{9,}$", RegexOptions.CultureInvariant);

    // DMS compatto come in itgeo.geo: tre cifre di gradi, due di primi, due di secondi, tre di millesimi.
    private static string Compatto(double gradi, string lettere, int cifreDeiGradi)
    {
        char lettera = gradi < 0 ? lettere[1] : lettere[0];
        long millesimi = (long)Math.Round(Math.Abs(gradi) * 3_600_000);
        long g = millesimi / 3_600_000, p = millesimi / 60_000 % 60, s = millesimi / 1000 % 60, m = millesimi % 1000;
        return string.Create(CultureInfo.InvariantCulture, $"{lettera}{g.ToString(new string('0', cifreDeiGradi), CultureInfo.InvariantCulture)}{p:00}{s:00}{m:000}");
    }

    /// <summary>
    /// L'anello <paramref name="nuovo"/> girato come <paramref name="vecchio"/>: nel suo verso — lo dicono i vertici
    /// vicini, quante coppie della copia la forma nuova ha nello stesso verso e quante al contrario, non l'area col segno,
    /// che su una forma che si incrocia non vuol dire niente (a pari, l'area) — e dal suo primo vertice, se c'è ancora,
    /// se no dal più vicino.
    /// </summary>
    public static IReadOnlyList<Coordinate> Giro(IReadOnlyList<Coordinate> nuovo, IReadOnlyList<Coordinate>? vecchio)
    {
        ArgumentNullException.ThrowIfNull(nuovo);
        IReadOnlyList<Coordinate> giro = nuovo;
        if (vecchio is not { Count: >= 3 })
            return giro;
        if (Verso(vecchio, giro) is var verso && (verso < 0 || verso == 0 && Math.Sign(Area(vecchio)) * Math.Sign(Area(giro)) < 0))
            giro = [.. giro.Reverse()];

        long primo = FormeUguali.Chiave(vecchio[0]);
        int dove = giro.ToList().FindIndex(p => FormeUguali.Chiave(p) == primo);
        if (dove < 0)
            dove = giro.Select((p, i) => (Distanza: Distanza(p, vecchio[0]), i)).MinBy(x => x.Distanza).i;
        return [.. giro.Skip(dove), .. giro.Take(dove)];
    }

    /// <summary>I punti senza le ripetizioni di seguito e senza quello che chiude l'anello.</summary>
    private static List<Coordinate> SenzaRipetuti(IReadOnlyList<Coordinate> punti)
    {
        var anello = new List<Coordinate>(punti.Count);
        foreach (var punto in punti)
        {
            if (anello.Count == 0 || FormeUguali.Chiave(anello[^1]) != FormeUguali.Chiave(punto))
                anello.Add(punto);
        }

        if (anello.Count > 1 && FormeUguali.Chiave(anello[0]) == FormeUguali.Chiave(anello[^1]))
            anello.RemoveAt(anello.Count - 1);
        return anello;
    }

    /// <summary>Il punto come la copia lo sa scrivere: per nome se ammette i nomi, se no con le sue coordinate.</summary>
    private static Punto? Scrivibile(Punto punto, Coordinate posizione, ElencoDiVertici copia)
    {
        if (punto.PerNome && copia.AmmetteNomi && copia.NonVa(punto) is null)
            return punto;
        var coordinate = Punto.Da(posizione);
        return copia.AmmetteCoordinate && copia.NonVa(coordinate) is null ? coordinate : null;
    }

    /// <summary>Positivo se la copia gira come la forma nuova, negativo se al contrario: coppie di vicini d'accordo meno quelle girate.</summary>
    private static int Verso(IReadOnlyList<Coordinate> copia, IReadOnlyList<Coordinate> nuovo)
    {
        var coppie = new HashSet<(long, long)>();
        for (int i = 0; i < nuovo.Count; i++)
            coppie.Add((FormeUguali.Chiave(nuovo[i]), FormeUguali.Chiave(nuovo[(i + 1) % nuovo.Count])));
        int avanti = 0, indietro = 0;
        for (int i = 0; i < copia.Count; i++)
        {
            long a = FormeUguali.Chiave(copia[i]), b = FormeUguali.Chiave(copia[(i + 1) % copia.Count]);
            if (coppie.Contains((a, b)))
                avanti++;
            if (coppie.Contains((b, a)))
                indietro++;
        }

        return avanti - indietro;
    }

    // Area col segno (formula del laccio, in gradi): il segno dice il verso del giro.
    private static double Area(IReadOnlyList<Coordinate> anello)
    {
        double somma = 0;
        for (int i = 0; i < anello.Count; i++)
        {
            var a = anello[i];
            var b = anello[(i + 1) % anello.Count];
            somma += (a.LongitudeDeg * b.LatitudeDeg) - (b.LongitudeDeg * a.LatitudeDeg);
        }

        return somma;
    }

    private static double Distanza(Coordinate a, Coordinate b)
    {
        double dy = a.LatitudeDeg - b.LatitudeDeg;
        double dx = (a.LongitudeDeg - b.LongitudeDeg) * Math.Cos(a.LatitudeDeg * Math.PI / 180);
        return (dx * dx) + (dy * dy);
    }
}
