using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Un record nascosto fra gli altri: le sue righe sono commentate, e per il motore sono commenti. Il Lab lo ritrova
/// togliendo i <c>//</c> e rileggendo (<see cref="Nascosti.Analizza"/>).
/// </summary>
/// <param name="Riga">La prima riga (da 1) nel file com'è adesso.</param>
/// <param name="Righe">Quante righe, dalla prima: quelle che «Mostra» scommenta.</param>
/// <param name="DopoIlRecord">L'indice del record che lo precede nel file (-1 = in testa): dove sta nell'elenco.</param>
/// <param name="Etichetta">Il nome che avrebbe, mostrato.</param>
public sealed record BloccoNascosto(int Riga, int Righe, int DopoIlRecord, string Etichetta);

/// <summary>
/// Quel che un file ha di nascosto (<see cref="Nascosti.Analizza"/>): i record tutti commentati fuori dal motore
/// (<see cref="Fra"/>), quelli che il motore tiene anche commentati (<see cref="Dentro"/>: le zone MVA, e i file a una riga
/// per record, dove una riga commentata è un record disattivato), e i record attivi con qualche riga commentata
/// (<see cref="InParte"/>: un'etichetta MVA commentata, i vertici di un tratto). Numeri di riga da 0.
/// </summary>
public sealed record AnalisiDeiNascosti(
    IReadOnlyList<BloccoNascosto> Fra,
    IReadOnlySet<int> Dentro,
    IReadOnlyDictionary<int, IReadOnlyList<int>> InParte)
{
    public static AnalisiDeiNascosti Vuota { get; } = new([], new HashSet<int>(), new Dictionary<int, IReadOnlyList<int>>());
}

/// <summary>
/// Nascondi e mostra (lotto «Subito» slice 5c, «file per file» B3, R-5, K4): un record si nasconde commentando con
/// <c>//</c> ogni sua riga di dati, e si mostra togliendoli. In Aurora un record nascosto non c'è; nel Lab resta
/// nell'elenco, grigio, e si rimostra con un clic. Come spezza e unisci, il gesto si fa sul testo e il motore rilegge.
/// </summary>
public static class Nascosti
{
    /// <summary>Una riga commentata che, tolto il <c>//</c>, è una riga di dati (ha un <c>;</c>); mai un tag <c>//@</c>.</summary>
    public static bool Commentata(string riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        string t = riga.TrimStart();
        return t.StartsWith("//", StringComparison.Ordinal) && !t.StartsWith("//@", StringComparison.Ordinal)
            && t[2..].TrimStart() is { Length: > 0 } dentro && !dentro.StartsWith("//", StringComparison.Ordinal) && dentro.Contains(';', StringComparison.Ordinal);
    }

    /// <summary>La riga senza il suo primo <c>//</c> (gli spazi davanti restano): il contrario di <see cref="Copri"/>.</summary>
    public static string Scopri(string riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        int dove = riga.IndexOf("//", StringComparison.Ordinal);
        return dove < 0 ? riga : riga[..dove] + riga[(dove + 2)..];
    }

    /// <summary>La riga commentata: <c>//</c> davanti, come la scrivono i file (<c>//L;LIRR;…</c>).</summary>
    public static string Copri(string riga) => "//" + riga;

    /// <summary>
    /// Quel che il file ha di nascosto, nel testo com'è adesso: si tolgono i <c>//</c> a tutte le righe commentate che
    /// hanno dati, si rilegge UNA volta, e si guarda ogni record che ne esce. Fatto solo di righe che erano commentate e
    /// fuori da un record nascosto dentro → nascosto fra gli altri; con righe attive e commentate insieme → in parte. La
    /// struttura del file resta com'era.
    /// </summary>
    public static AnalisiDeiNascosti Analizza(FileAperto file, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(posti);
        if (file is not IFileConRecord conRecord || file.Relativo.EndsWith(".atis", StringComparison.OrdinalIgnoreCase))
            return AnalisiDeiNascosti.Vuota;
        var commentate = Enumerable.Range(0, righe.Count).Where(i => Commentata(righe[i])).ToHashSet();
        if (commentate.Count == 0)
            return AnalisiDeiNascosti.Vuota;

        // I record di adesso tutti commentati (il motore li tiene: zone MVA, righe disattivate) e, per ogni riga, il
        // record di adesso che la contiene.
        var dentro = Enumerable.Range(0, posti.Count).Where(i => Dentro(righe, posti[i])).ToHashSet();
        var diChi = new int[righe.Count];
        Array.Fill(diChi, -1);
        for (int r = 0; r < posti.Count; r++)
        {
            for (int i = posti[r].Da; i < posti[r].Da + posti[r].Quante && i < righe.Count; i++)
                diChi[i] = r;
        }

        var scoperte = righe.Select((r, i) => commentate.Contains(i) ? Scopri(r) : r).ToList();
        var adesso = conRecord.IstantaneaDellaStruttura();
        var fra = new List<BloccoNascosto>();
        var inParte = new Dictionary<int, List<int>>();
        try
        {
            conRecord.RipristinaLaStruttura(conRecord.LeggiLeRighe(scoperte));
            IReadOnlyList<string>? etichette = null;
            foreach (var (posto, r) in conRecord.PostiDeiRecord([]).Select((p, r) => (p, r)))
            {
                var suoi = Enumerable.Range(posto.Da, posto.Quante).Where(i => i < righe.Count).ToList();
                var dati = suoi.Where(i => scoperte[i].Trim() is { Length: > 0 } t && !t.StartsWith("//", StringComparison.Ordinal)).ToList();
                var nascoste = dati.Where(commentate.Contains).ToList();
                if (nascoste.Count == 0 || nascoste.Any(i => diChi[i] >= 0 && dentro.Contains(diChi[i])))
                    continue;

                if (nascoste.Count == dati.Count)
                {
                    etichette ??= Ispettore.Etichette(file, null);
                    int prima = posti.Count(p => p.Da <= posto.Da) - 1;
                    fra.Add(new BloccoNascosto(posto.Da + 1, posto.Quante, prima, etichette.ElementAtOrDefault(r) ?? ""));
                }
                else if (dati.Where(i => !commentate.Contains(i)).Select(i => diChi[i]).Distinct().ToList() is [var suo and >= 0])
                {
                    // Righe attive tutte dello stesso record di adesso: le commentate sono sue, nascoste dentro di lui.
                    if (!inParte.TryGetValue(suo, out var sue))
                        inParte[suo] = sue = [];
                    sue.AddRange(nascoste);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new AnalisiDeiNascosti([], dentro, new Dictionary<int, IReadOnlyList<int>>());
        }
        finally
        {
            conRecord.RipristinaLaStruttura(adesso);
        }

        return new AnalisiDeiNascosti(fra, dentro, inParte.ToDictionary(p => p.Key, p => (IReadOnlyList<int>)p.Value));
    }

    /// <summary>
    /// Vero se il record è nascosto DENTRO: nessuna riga di dati attiva (i separatori <c>T;DUMMY</c> non contano) e
    /// almeno una commentata. Le zone MVA e i file a una riga per record tengono le righe commentate nel record.
    /// </summary>
    public static bool Dentro(IReadOnlyList<string> righe, (int Da, int Quante) posto)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var suoi = Enumerable.Range(posto.Da, posto.Quante).Where(i => i < righe.Count).Select(i => righe[i]).ToList();
        return suoi.Any(Commentata) && !suoi.Any(r => r.Trim() is { Length: > 0 } t && !t.StartsWith("//", StringComparison.Ordinal) && !EUnSeparatore(t));
    }

    /// <summary>
    /// Le righe da cambiare per nascondere il record: <c>//</c> davanti a ogni riga di dati. Nelle MVA il separatore
    /// <c>T;DUMMY</c> resta attivo, come nelle zone nascoste del fork (chiude il blocco).
    /// </summary>
    internal static Dictionary<int, IReadOnlyList<string>> Nascondi(object record, IReadOnlyList<string> righe, (int Da, int Quante) posto)
    {
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        for (int i = posto.Da; i < posto.Da + posto.Quante && i < righe.Count; i++)
        {
            string t = righe[i].Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal) || record is MvaSector && EUnSeparatore(t))
                continue;
            sostituzioni[i + 1] = [Copri(righe[i])];
        }

        return sostituzioni;
    }

    /// <summary>Le righe da cambiare per mostrare: via il <c>//</c> da quelle (da 0) date.</summary>
    internal static Dictionary<int, IReadOnlyList<string>> Mostra(IReadOnlyList<string> righe, IEnumerable<int> quali)
        => quali.Where(i => i >= 0 && i < righe.Count && Commentata(righe[i]))
                .ToDictionary(i => i + 1, i => (IReadOnlyList<string>)[Scopri(righe[i])]);

    private static bool EUnSeparatore(string riga)
    {
        var campi = riga.Split(';');
        return campi.Length > 1 && campi[0].Trim() is "T" or "t" && string.Equals(campi[1].Trim(), "DUMMY", StringComparison.OrdinalIgnoreCase);
    }
}
