using System.Text;
using System.Text.RegularExpressions;

namespace Vipi.Sectorfile.IO;

/// <summary>Un pezzo di un modello ATIS: testo, segnaposto o parte facoltativa.</summary>
public abstract record PezzoDelModello;

/// <summary>Testo scritto com'è (per la voce, nei <c>.atis</c>: «aitis», «Q F Echo»).</summary>
public sealed record TestoDelModello(string Testo) : PezzoDelModello;

/// <summary>Un segnaposto, <c>[ATIS_LETTER]</c>: Aurora ci mette il valore.</summary>
public sealed record SegnapostoDelModello(string Nome) : PezzoDelModello;

/// <summary>
/// Una parte facoltativa, <c>[Arrival runway [ARR]]</c>: c'è solo se i suoi segnaposto hanno un valore. Può contenerne
/// altre.
/// </summary>
public sealed record ParteFacoltativa(IReadOnlyList<PezzoDelModello> Pezzi) : PezzoDelModello;

/// <summary>Un modello letto: i suoi pezzi, e dove le parentesi non tornano (posizioni nel testo, da 0).</summary>
/// <param name="ChiuseInPiu">Le <c>]</c> che non chiudono niente.</param>
/// <param name="MaiChiuse">Le <c>[</c> che nessuna <c>]</c> chiude.</param>
public sealed record ModelloAtisLetto(IReadOnlyList<PezzoDelModello> Pezzi, IReadOnlyList<int> ChiuseInPiu, IReadOnlyList<int> MaiChiuse)
{
    public bool Bilanciato => ChiuseInPiu.Count == 0 && MaiChiuse.Count == 0;

    /// <summary>I segnaposto nell'ordine in cui compaiono, ripetuti se si ripetono.</summary>
    public IReadOnlyList<string> Segnaposto { get; } = [.. Dentro(Pezzi)];

    private static IEnumerable<string> Dentro(IEnumerable<PezzoDelModello> pezzi)
        => pezzi.SelectMany(p => p switch
        {
            SegnapostoDelModello s => [s.Nome],
            ParteFacoltativa f => Dentro(f.Pezzi),
            _ => Enumerable.Empty<string>(),
        });
}

/// <summary>
/// I modelli di testo dell'ATIS e del D-ATIS (lotto «Subito» slice 18a; «file per file» §22): una riga coi segnaposto
/// fra parentesi quadre e le parti facoltative, anche annidate.
/// </summary>
/// <remarks>
/// <para>Il manuale IVAO di <c>[ATIS]</c> rimanda allo strumento «ATIS Creator», che conosce dieci segnaposto; i modelli
/// italiani usano anche <c>QFE</c> e <c>CPDLC</c> (più recenti dello strumento) e <c>ARR_TYPE</c>, dichiarato da
/// <c>atisextra.fds</c> (sezione <c>[ATISFIELD]</c>, che il manuale non descrive: <c>Etichetta;[SEGNAPOSTO];</c>).</para>
/// <para>Una parentesi che contiene solo un nome in maiuscole (<c>[TL]</c>) è un segnaposto; una che contiene altro è
/// una parte facoltativa. Sul fork: 7 <c>.atis</c> e 4 <c>.datis</c> (uno vuoto, voluto), tutti di una riga.</para>
/// </remarks>
public static partial class ModelloAtis
{
    /// <summary>
    /// I segnaposto che Aurora riempie da sé, col loro significato: i dieci di ATIS Creator, più <c>QFE</c> e
    /// <c>CPDLC</c>. Quelli dei <c>.fds</c> si aggiungono a questi.
    /// </summary>
    public static IReadOnlyList<(string Nome, string Significato)> DiAurora { get; } =
    [
        ("STATION_NAME", "il nome della stazione"),
        ("ATIS_LETTER", "la lettera dell'informazione"),
        ("ATIS_TIME", "l'ora dell'informazione"),
        ("ARR", "la pista (o le piste) di arrivo"),
        ("DEP", "la pista (o le piste) di partenza"),
        ("DEP_FREQ", "la frequenza delle partenze"),
        ("TA", "l'altitudine di transizione"),
        ("TL", "il livello di transizione"),
        ("METAR", "il bollettino"),
        ("REMARK", "le note scritte dal controllore"),
        ("QFE", "il QFE"),
        ("CPDLC", "l'identificativo CPDLC (nei D-ATIS)"),
    ];

    /// <summary>Legge un modello. Non fallisce mai: le parentesi che non tornano restano testo, e si dice dove sono.</summary>
    public static ModelloAtisLetto Leggi(string modello)
    {
        ArgumentNullException.ThrowIfNull(modello);
        var chiuseInPiu = new List<int>();
        var maiChiuse = new List<int>();
        // In cima alla pila il livello aperto: i suoi pezzi e la posizione della sua «[» (-1 per il modello intero).
        var pila = new Stack<(List<PezzoDelModello> Pezzi, int Aperta)>();
        pila.Push(([], -1));
        var testo = new StringBuilder();

        void ChiudiIlTesto()
        {
            if (testo.Length > 0)
            {
                pila.Peek().Pezzi.Add(new TestoDelModello(testo.ToString()));
                testo.Clear();
            }
        }

        for (int i = 0; i < modello.Length; i++)
        {
            char c = modello[i];
            if (c == '[')
            {
                ChiudiIlTesto();
                pila.Push(([], i));
            }
            else if (c == ']' && pila.Count > 1)
            {
                ChiudiIlTesto();
                var (pezzi, _) = pila.Pop();
                pila.Peek().Pezzi.Add(pezzi is [TestoDelModello solo] && Nome().IsMatch(solo.Testo)
                    ? new SegnapostoDelModello(solo.Testo)
                    : new ParteFacoltativa(pezzi));
            }
            else
            {
                if (c == ']')
                    chiuseInPiu.Add(i);
                testo.Append(c);
            }
        }

        ChiudiIlTesto();
        // Una «[» mai chiusa: resta testo, coi pezzi che aveva dentro al livello di sopra.
        while (pila.Count > 1)
        {
            var (pezzi, aperta) = pila.Pop();
            maiChiuse.Insert(0, aperta);
            pila.Peek().Pezzi.Add(new TestoDelModello("["));
            pila.Peek().Pezzi.AddRange(pezzi);
        }

        return new ModelloAtisLetto(pila.Pop().Pezzi, chiuseInPiu, maiChiuse);
    }

    /// <summary>
    /// Il testo che esce dal modello con quei valori: un segnaposto col valore vuoto sparisce, e con lui la parte
    /// facoltativa che lo tiene; un segnaposto che non è fra i <paramref name="valori"/> resta scritto com'è
    /// (<c>[NUOVO]</c>), così si vede. Gli spazi doppi che restano si stringono.
    /// </summary>
    public static string Riempi(ModelloAtisLetto modello, IReadOnlyDictionary<string, string> valori)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(valori);
        return SpaziDoppi().Replace(Riempi(modello.Pezzi, valori), " ").Trim();
    }

    private static string Riempi(IReadOnlyList<PezzoDelModello> pezzi, IReadOnlyDictionary<string, string> valori)
    {
        var uscita = new StringBuilder();
        foreach (var pezzo in pezzi)
        {
            switch (pezzo)
            {
                case TestoDelModello t:
                    uscita.Append(t.Testo);
                    break;
                case SegnapostoDelModello s:
                    uscita.Append(valori.TryGetValue(s.Nome, out string? valore) ? valore : $"[{s.Nome}]");
                    break;
                case ParteFacoltativa f when f.Pezzi.OfType<SegnapostoDelModello>().All(s => !valori.TryGetValue(s.Nome, out string? suo) || suo.Trim().Length > 0):
                    uscita.Append(Riempi(f.Pezzi, valori));
                    break;
            }
        }

        return uscita.ToString();
    }

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$")]
    private static partial Regex Nome();

    [GeneratedRegex(@" {2,}")]
    private static partial Regex SpaziDoppi();
}
