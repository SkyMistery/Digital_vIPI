using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>Dove è andata la forma dopo un gesto: le copie che l'hanno ricevuta, e quelle no col perché.</summary>
public sealed record FormaPortataSulleCopie(IReadOnlyList<ParteDiForma> Portate, IReadOnlyList<(ParteDiForma Copia, string Perche)> NonPortate);

/// <summary>
/// Portare una forma su una sua copia (lotto «Subito» slice 8c, «file per file» D5): dopo un gesto sui vertici la
/// stessa forma va sulle copie che erano uguali, e «allinea questa» / «prendi la sua» la portano su una copia diversa.
/// </summary>
/// <remarks>
/// La copia prende l'ANELLO della forma, non le sue righe: tiene il suo vertice di partenza, il suo verso (orario o
/// antiorario), la sua chiusura (il primo punto ripetuto in fondo, se c'era) e la scrittura dei vertici che restano —
/// un punto per nome resta per nome, una coordinata compatta resta compatta. Un vertice nuovo si scrive come lo scrive
/// la forma di partenza, se la copia lo sa scrivere così (un <c>.pol</c> tiene solo coordinate: il nome diventa la sua
/// posizione; i punti di una procedura <c>.str</c> solo nomi: una coordinata non ci va, e la copia si rifiuta).
/// <para>Due vertici sono lo stesso al decimo di metro, come in <see cref="FormeUguali"/>.</para>
/// </remarks>
public static class PortaLaForma
{
    /// <summary>
    /// L'elenco dei vertici che disegna la parte <paramref name="parte"/> del record (la stessa numerazione delle forme
    /// della mappa): il poligono n-esimo di un confine, il tratto n-esimo non vuoto di una zona <c>.str</c>, l'unico
    /// elenco degli altri. Null dove la parte non è un elenco di vertici (le linee dei <c>.geo</c>: slice 8d).
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

    /// <summary>L'anello di un elenco (i punti per nome risolti nel catalogo), o null se un nome non si risolve.</summary>
    public static IReadOnlyList<Coordinate>? Anello(ElencoDiVertici elenco, IFixResolver? catalogo)
    {
        ArgumentNullException.ThrowIfNull(elenco);
        var punti = new List<Coordinate>(elenco.Quanti);
        var chiavi = new List<long>(elenco.Quanti);
        for (int i = 0; i < elenco.Quanti; i++)
        {
            if (!elenco.PuntoDi(i).TryRisolvi(catalogo, out var posizione))
                return null;
            long chiave = FormeUguali.Chiave(posizione);
            if (chiavi.Count > 0 && chiavi[^1] == chiave)
                continue;
            punti.Add(posizione);
            chiavi.Add(chiave);
        }

        if (chiavi.Count > 1 && chiavi[0] == chiavi[^1])
            punti.RemoveAt(punti.Count - 1);
        return punti;
    }

    /// <summary>Vero se i due elenchi disegnano lo stesso anello.</summary>
    public static bool Uguali(ElencoDiVertici a, ElencoDiVertici b, IFixResolver? catalogo)
        => Anello(a, catalogo) is { } primo && Anello(b, catalogo) is { } secondo
           && FormeUguali.UgualiComeAnello([.. primo.Select(FormeUguali.Chiave)], [.. secondo.Select(FormeUguali.Chiave)]);

    /// <summary>
    /// Le voci nuove della copia: l'anello di <paramref name="da"/> scritto come lo scriverebbe <paramref name="copia"/>.
    /// Torna il motivo, se la copia non lo sa scrivere.
    /// </summary>
    public static IReadOnlyList<object>? VociPer(ElencoDiVertici da, ElencoDiVertici copia, IFixResolver? catalogo, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(da);
        ArgumentNullException.ThrowIfNull(copia);
        perche = null;
        if (Anello(da, catalogo) is not { Count: >= 3 } nuovo)
        {
            perche = "La forma di partenza ha un nome che non si risolve, o meno di 3 vertici.";
            return null;
        }

        var vecchio = Anello(copia, catalogo);
        // Il verso della copia: se era girato al contrario, resta girato al contrario. Lo dicono i vertici vicini — quante
        // coppie della copia la forma nuova ha nello stesso verso e quante al contrario —, non l'area col segno, che su
        // una forma che si incrocia non vuol dire niente. A pari, l'area.
        if (vecchio is { Count: >= 3 } && Verso(vecchio, nuovo) is var verso && (verso < 0 || verso == 0 && Math.Sign(Area(vecchio)) * Math.Sign(Area(nuovo)) < 0))
            nuovo = [.. nuovo.AsEnumerable().Reverse()];

        // La partenza della copia: il suo primo vertice se c'è ancora, se no il più vicino.
        if (vecchio is { Count: > 0 })
        {
            long primo = FormeUguali.Chiave(vecchio[0]);
            int dove = nuovo.ToList().FindIndex(p => FormeUguali.Chiave(p) == primo);
            if (dove < 0)
                dove = nuovo.Select((p, i) => (Distanza: Distanza(p, vecchio[0]), i)).MinBy(x => x.Distanza).i;
            nuovo = [.. nuovo.Skip(dove), .. nuovo.Take(dove)];
        }

        // Le voci della copia per posizione (quella che chiude l'anello a parte), e quelle della forma di partenza.
        var dellaCopia = new Dictionary<long, object>();
        var perDa = new Dictionary<long, Punto>();
        for (int i = 0; i < copia.Quanti; i++)
        {
            if (copia.PuntoDi(i).TryRisolvi(catalogo, out var c))
                dellaCopia.TryAdd(FormeUguali.Chiave(c), copia.Elenco[i]!);
        }

        for (int i = 0; i < da.Quanti; i++)
        {
            var punto = da.PuntoDi(i);
            if (punto.TryRisolvi(catalogo, out var c))
                perDa.TryAdd(FormeUguali.Chiave(c), punto);
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

            if (Scrivibile(perDa.GetValueOrDefault(chiave, Punto.Da(posizione)), posizione, copia) is not { } punto)
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
                    : Scrivibile(perDa.GetValueOrDefault(partenza, Punto.Da(nuovo[0])), nuovo[0], copia) ?? Punto.Da(nuovo[0]), vecchio: null));
        }

        return voci;
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
