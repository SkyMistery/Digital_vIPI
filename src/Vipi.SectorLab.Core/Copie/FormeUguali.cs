using Vipi.SectorLab.Core.Mappa;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>Un pezzo di forma: il tratto <paramref name="Parte"/> del record (un poligono di un confine, un tratto di una mappa).</summary>
public readonly record struct ParteDiForma(string File, int Record, int Parte, string Etichetta);

/// <summary>
/// Un'altra forma che ha gli stessi vertici di quella della scheda: <see cref="Uguale"/> se è lo stesso anello (inizio e
/// verso qualsiasi), altrimenti «simile» — almeno 9 vertici su 10 in comune — con quanti vertici ha in più o in meno.
/// </summary>
public sealed record CopiaDellaForma(ParteDiForma Dove, bool Uguale, int SoloQui, int SoloLa, int Vertici);

/// <summary>
/// Le famiglie di forme (lotto «Subito» slice 8, «file per file» D5, I2, J3, Q5, H10): la stessa forma disegnata in più
/// record — il settore dinamico e il suo confine in <c>.hartcc</c>, l'ATZ nel <c>MAPS</c> di uno <c>.str</c>, il bordo
/// in un <c>.geo</c> e il riempimento in un <c>.pol</c>. La famiglia la trova il Lab dalla geometria: due forme sono
/// la stessa se hanno gli stessi vertici nello stesso giro, da qualunque vertice si parta e in qualunque verso (D5:
/// confronto come ANELLO), in qualunque forma siano scritte le coordinate (DMS col punto, compatte, per nome: si
/// confrontano le posizioni risolte).
/// </summary>
/// <remarks>
/// Si confrontano le forme della mappa (<see cref="FormaDellaMappa"/>): i punti per nome sono già risolti nel master
/// scelto, i segmenti dei <c>.geo</c> già cuciti in polilinee. Il vertice che chiude l'anello ripetendo il primo non
/// conta (Aurora chiude da sola i poligoni), e nemmeno un vertice ripetuto di seguito. Due vertici sono lo stesso se
/// coincidono al decimo di metro: il sector scrive i millesimi di secondo (3 cm).
/// <para>Una forma con meno di 3 vertici non è una forma: una linea di due punti coincide per caso con troppe altre.</para>
/// </remarks>
public sealed class FormeUguali
{
    /// <summary>Sotto questa quota di vertici in comune (sul più lungo dei due anelli) due forme non sono parenti.</summary>
    public const double Soglia = 0.9;

    private readonly List<Anello> _anelli = [];
    private readonly Dictionary<(string File, int Record), List<int>> _perRecord = new(new ConfrontoDelRecord());
    private readonly Dictionary<string, List<int>> _primiDelFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, List<int>> _perVertice = [];

    private FormeUguali(IEnumerable<FormaDellaMappa> forme)
    {
        foreach (var forma in forme)
        {
            if (forma.Tipo == TipoDiForma.Punto)
                continue;
            for (int t = 0; t < forma.Tratti.Count; t++)
            {
                if (Chiavi(forma.Tratti[t]) is not { } chiavi)
                    continue;
                int numero = _anelli.Count;
                int parte = forma.Parti?[t] ?? t;
                _anelli.Add(new Anello(new ParteDiForma(forma.File, forma.Record, parte, forma.Etichetta), chiavi, [.. chiavi.Distinct()]));
                var chiave = (forma.File, forma.Record);
                if (!_perRecord.TryGetValue(chiave, out var suoi))
                {
                    _perRecord[chiave] = suoi = [];
                    if (!_primiDelFile.TryGetValue(forma.File, out var primi))
                        _primiDelFile[forma.File] = primi = [];
                    primi.Add(forma.Record);
                }

                suoi.Add(numero);
                foreach (long vertice in _anelli[numero].Distinti)
                {
                    if (!_perVertice.TryGetValue(vertice, out var chi))
                        _perVertice[vertice] = chi = [];
                    chi.Add(numero);
                }
            }
        }

        foreach (var primi in _primiDelFile.Values)
            primi.Sort();
    }

    /// <summary>L'indice delle forme di tutti gli strati della mappa, come sono adesso.</summary>
    public static FormeUguali Di(IEnumerable<StratoDellaMappa> strati)
    {
        ArgumentNullException.ThrowIfNull(strati);
        return new FormeUguali(strati.SelectMany(s => s.Forme));
    }

    /// <summary>L'indice di un elenco di forme (le prove, la misura).</summary>
    public static FormeUguali Di(IEnumerable<FormaDellaMappa> forme)
    {
        ArgumentNullException.ThrowIfNull(forme);
        return new FormeUguali(forme);
    }

    /// <summary>Quante forme (anelli di almeno 3 vertici) sono nell'indice.</summary>
    public int Forme => _anelli.Count;

    /// <summary>
    /// Le parti del record che hanno una copia, ognuna con le sue copie: prima le uguali, poi le simili, per file.
    /// Un segmento di un <c>.geo</c> vale per la linea di cui fa parte (la mappa cuce i segmenti in polilinee).
    /// </summary>
    public IReadOnlyList<(ParteDiForma Parte, IReadOnlyList<CopiaDellaForma> Copie)> Di(string file, int record)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (Suoi(file, record) is not { } suoi)
            return [];

        var risposta = new List<(ParteDiForma, IReadOnlyList<CopiaDellaForma>)>();
        foreach (int numero in suoi)
        {
            var copie = CopieDi(numero);
            if (copie.Count > 0)
                risposta.Add((_anelli[numero].Dove, copie));
        }

        return risposta;
    }

    /// <summary>
    /// Confronta i membri di una famiglia dichiarata (<c>form=</c>, slice 8b): la forma della famiglia è l'anello che hanno
    /// più membri (a pari, quello del primo); ogni membro ha la sua parte uguale, o null se non ce l'ha — o se non si
    /// disegna. Due record che sono la stessa linea di un <c>.geo</c> contano una volta sola (il primo dei due).
    /// </summary>
    public IReadOnlyList<(string File, int Record, ParteDiForma? Parte)> Confronta(IEnumerable<(string File, int Record)> membri)
    {
        ArgumentNullException.ThrowIfNull(membri);
        var unici = new List<(string File, int Record, List<int> Anelli)>();
        var visti = new HashSet<List<int>>(ReferenceEqualityComparer.Instance);
        foreach (var (file, record) in membri)
        {
            var suoi = Suoi(file, record);
            if (suoi is null)
                unici.Add((file, record, []));
            else if (visti.Add(suoi))
                unici.Add((file, record, suoi));
        }

        int? scelto = null;
        int migliore = 0;
        foreach (int candidato in unici.SelectMany(m => m.Anelli))
        {
            int quanti = unici.Count(m => m.Anelli.Any(a => UgualiComeAnello(_anelli[a].Chiavi, _anelli[candidato].Chiavi)));
            if (quanti > migliore)
                (scelto, migliore) = (candidato, quanti);
        }

        return [.. unici.Select(m => (m.File, m.Record,
            scelto is { } forma && m.Anelli.FirstOrDefault(a => UgualiComeAnello(_anelli[a].Chiavi, _anelli[forma].Chiavi), -1) is var uguale and >= 0
                ? _anelli[uguale].Dove
                : (ParteDiForma?)null))];
    }

    /// <summary>
    /// Le famiglie di forme uguali: ogni gruppo di anelli uguali fra loro in almeno due record. Per la misura e per il
    /// controllo delle famiglie dichiarate (<c>form=</c>).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<ParteDiForma>> Famiglie()
    {
        var padre = Enumerable.Range(0, _anelli.Count).ToArray();
        int Radice(int x)
        {
            while (padre[x] != x)
                x = padre[x] = padre[padre[x]];
            return x;
        }

        var inFamiglia = new HashSet<int>();
        for (int i = 0; i < _anelli.Count; i++)
        {
            foreach (int j in Parenti(i).Where(j => j > i && UgualiComeAnello(_anelli[i].Chiavi, _anelli[j].Chiavi)))
            {
                padre[Radice(i)] = Radice(j);
                inFamiglia.Add(i);
                inFamiglia.Add(j);
            }
        }

        return [.. inFamiglia.GroupBy(Radice)
            .Select(g => (IReadOnlyList<ParteDiForma>)[.. g.Order().Select(n => _anelli[n].Dove)])
            .OrderBy(f => f[0].File, StringComparer.Ordinal).ThenBy(f => f[0].Record)];
    }

    /// <summary>Gli anelli di un record: i suoi, o quelli della linea del <c>.geo</c> di cui il segmento fa parte.</summary>
    private List<int>? Suoi(string file, int record)
    {
        if (_perRecord.TryGetValue((file, record), out var suoi))
            return suoi;
        // Una linea dei .geo (e delle aree P/R/D) è agganciata al suo PRIMO segmento: gli altri stanno fra quello e il
        // primo della linea dopo.
        if (!_primiDelFile.TryGetValue(file, out var primi))
            return null;
        int dove = primi.BinarySearch(record);
        int prima = dove >= 0 ? dove : ~dove - 1;
        return prima >= 0 && _perRecord.TryGetValue((file, primi[prima]), out var linea) && ELinea(file) ? linea : null;
    }

    // Solo i file a segmenti si cuciono: in un .tfl il record dopo un settore è un altro settore, non un suo pezzo.
    private static bool ELinea(string file)
        => Path.GetExtension(file).ToLowerInvariant() is ".geo" or ".restrict" or ".prohibit" or ".danger";

    private List<CopiaDellaForma> CopieDi(int numero)
    {
        var mio = _anelli[numero];
        var copie = new List<CopiaDellaForma>();
        foreach (int j in Parenti(numero))
        {
            var altro = _anelli[j];
            if (altro.Dove.Record == mio.Dove.Record && string.Equals(altro.Dove.File, mio.Dove.File, StringComparison.OrdinalIgnoreCase))
                continue;
            int comuni = mio.Distinti.Intersect(altro.Distinti).Count();
            copie.Add(new CopiaDellaForma(altro.Dove, UgualiComeAnello(mio.Chiavi, altro.Chiavi),
                mio.Distinti.Length - comuni, altro.Distinti.Length - comuni, altro.Distinti.Length));
        }

        return [.. copie.OrderByDescending(c => c.Uguale).ThenBy(c => c.SoloQui + c.SoloLa)
            .ThenBy(c => c.Dove.File, StringComparer.Ordinal).ThenBy(c => c.Dove.Record).ThenBy(c => c.Dove.Parte)];
    }

    /// <summary>Gli anelli (non questo) con almeno 9 vertici su 10 in comune con lui, sul più lungo dei due.</summary>
    private IEnumerable<int> Parenti(int numero)
    {
        var mio = _anelli[numero];
        var comuni = new Dictionary<int, int>();
        foreach (long vertice in mio.Distinti)
        {
            foreach (int j in _perVertice[vertice])
            {
                if (j != numero)
                    comuni[j] = comuni.GetValueOrDefault(j) + 1;
            }
        }

        return comuni.Where(c => c.Value >= Soglia * Math.Max(mio.Distinti.Length, _anelli[c.Key].Distinti.Length))
            .Select(c => c.Key).Order();
    }

    /// <summary>I vertici di un tratto come chiavi (al decimo di metro), senza le ripetizioni e senza il vertice che chiude.</summary>
    private static long[]? Chiavi(IReadOnlyList<Coordinate> tratto)
    {
        var chiavi = new List<long>(tratto.Count);
        foreach (var punto in tratto)
        {
            long chiave = Chiave(punto);
            if (chiavi.Count == 0 || chiavi[^1] != chiave)
                chiavi.Add(chiave);
        }

        if (chiavi.Count > 1 && chiavi[0] == chiavi[^1])
            chiavi.RemoveAt(chiavi.Count - 1);
        return chiavi.Distinct().Count() >= 3 ? [.. chiavi] : null;
    }

    internal static long Chiave(Coordinate punto)
        => (long)Math.Round(punto.LatitudeDeg * 1e6) * 1_000_000_000L + (long)Math.Round(punto.LongitudeDeg * 1e6);

    /// <summary>Lo stesso anello: stessi vertici nello stesso giro, da qualunque vertice e in qualunque verso.</summary>
    internal static bool UgualiComeAnello(long[] a, long[] b)
    {
        if (a.Length != b.Length)
            return false;
        int n = a.Length;
        for (int inizio = 0; inizio < n; inizio++)
        {
            if (b[inizio] != a[0])
                continue;
            bool avanti = true, indietro = true;
            for (int k = 0; k < n && (avanti || indietro); k++)
            {
                avanti &= b[(inizio + k) % n] == a[k];
                indietro &= b[((inizio - k) % n + n) % n] == a[k];
            }

            if (avanti || indietro)
                return true;
        }

        return false;
    }

    private sealed record Anello(ParteDiForma Dove, long[] Chiavi, long[] Distinti);

    // I percorsi della sessione sono indifferenti a maiuscole e minuscole (è Windows): anche l'indice.
    private sealed class ConfrontoDelRecord : IEqualityComparer<(string File, int Record)>
    {
        public bool Equals((string File, int Record) a, (string File, int Record) b)
            => a.Record == b.Record && string.Equals(a.File, b.File, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string File, int Record) c)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(c.File), c.Record);
    }
}
