using System.Text;
using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>Una domanda di una prova: che cosa guardare, e dove.</summary>
public sealed record DomandaDellaProva(string Testo, string Dove);

/// <summary>Un file della variante: il percorso dalla radice del repo (barre in avanti) e il testo nuovo.</summary>
public sealed record FileDellaProva(string Percorso, string Testo);

/// <summary>La variante preparata: i file da scrivere, le domande, e che cosa ha fatto (per il messaggio e la carta).</summary>
public sealed record ProvaPreparata(IReadOnlyList<FileDellaProva> File, IReadOnlyList<DomandaDellaProva> Domande, IReadOnlyList<string> Fatto);

/// <summary>
/// Una prova in Aurora preparata dal Lab (carta F5, slice 4): al posto del <c>LEGGIMI</c> a mano, un ramo
/// <c>prova/&lt;argomento&gt;</c> con la variante da provare e un lotto già scritto con le domande. L'esito si scrive nel
/// lotto; il ramo si butta, e non si manda mai (<see cref="Invio"/> manda solo gli <c>aod/</c>).
/// </summary>
public abstract class ProvaInAurora
{
    /// <summary>L'argomento: la seconda metà del nome del ramo.</summary>
    public abstract string Argomento { get; }

    /// <summary>Il titolo, come lo legge l'AOD.</summary>
    public abstract string Titolo { get; }

    /// <summary>Perché la si fa: la voce della carta e cosa se ne decide.</summary>
    public abstract string Perche { get; }

    /// <summary>Prepara la variante dai file del sector in <paramref name="radice"/>, o null col perché.</summary>
    public abstract ProvaPreparata? Prepara(string radice, out string? perche);

    /// <summary>Il ramo della prova.</summary>
    public string Ramo => ProveInAurora.Prefisso + Argomento;
}

/// <summary>Le prove che il Lab sa preparare, e i rami <c>prova/…</c>.</summary>
public static class ProveInAurora
{
    /// <summary>Il prefisso dei rami di prova: solo sul PC dell'AOD, mai sul server.</summary>
    public const string Prefisso = "prova/";

    /// <summary>Le prove, in ordine.</summary>
    public static IReadOnlyList<ProvaInAurora> Tutte { get; } = [new OrdineABlocchiDelleAerovie()];

    /// <summary>La prova di quel ramo (<c>prova/…</c>), o null.</summary>
    public static ProvaInAurora? DelRamo(string? ramo)
        => ramo is null ? null : Tutte.FirstOrDefault(p => p.Ramo == ramo);

    /// <summary>
    /// Prepara la prova: dal ramo di adesso un ramo <c>prova/…</c>, la variante scritta e registrata lì (un commit solo
    /// locale). Torna la variante, o null col perché. Chi chiama ha già controllato che non ci sia niente da registrare.
    /// </summary>
    public static async Task<(ProvaPreparata? Preparata, string? Perche)> PreparaAsync(Git.Git git, string radice, ProvaInAurora prova, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(prova);
        var testa = await git.EseguiAsync(radice, ["rev-parse", "--abbrev-ref", "HEAD"], annulla).ConfigureAwait(false);
        string base_ = testa.Uscita.Trim();
        if (!testa.Riuscito || base_ is "HEAD" or "")
            return (null, "git è su un commit staccato: passa a un ramo prima di preparare una prova.");
        if (base_.StartsWith(Prefisso, StringComparison.Ordinal))
            return (null, $"Sei già su una prova ({base_}): buttala prima di prepararne un'altra.");
        if ((await RamiDelSector.LocaliAsync(git, radice, annulla).ConfigureAwait(false)).Contains(prova.Ramo, StringComparer.Ordinal))
            return (null, $"Il ramo {prova.Ramo} c'è già: passaci, o buttalo.");

        var preparata = prova.Prepara(radice, out string? perche);
        if (preparata is null)
            return (null, perche ?? "La variante non si è lasciata preparare.");

        var crea = await git.EseguiAsync(radice, ["switch", "--quiet", "-c", prova.Ramo], annulla).ConfigureAwait(false);
        if (!crea.Riuscito)
            return (null, $"git non ha creato il ramo: {crea.Errore}");
        // Da dove è partita (dove si torna buttandola): lo stesso segno dei rami di lavoro (RamiDelSector.BaseDiAsync).
        await git.EseguiAsync(radice, ["config", $"branch.{prova.Ramo}.vipibase", base_], annulla).ConfigureAwait(false);
        foreach (var file in preparata.File)
            await File.WriteAllTextAsync(Path.Combine(radice, file.Percorso), file.Testo, Encoding.Latin1, annulla).ConfigureAwait(false);

        var aggiungi = await git.EseguiAsync(radice, ["add", "--", .. preparata.File.Select(f => f.Percorso)], annulla).ConfigureAwait(false);
        var registra = aggiungi.Riuscito
            ? await git.EseguiAsync(radice, ["commit", "--quiet", "-m", $"Prova in Aurora: {prova.Titolo}", "-m",
                $"Ramo di prova del Sector Lab, solo su questo PC: non si manda.\n\n{string.Join("\n", preparata.Fatto)}"], annulla).ConfigureAwait(false)
            : aggiungi;
        return registra.Riuscito ? (preparata, null) : (null, $"git non ha registrato la variante: {registra.Errore}");
    }

    /// <summary>
    /// Butta la prova: torna al ramo da cui era partita e cancella il ramo <c>prova/…</c> (solo locale: non è mai stato
    /// mandato). Torna null se è andata, o il perché.
    /// </summary>
    public static async Task<string?> ButtaAsync(Git.Git git, string radice, string ramo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (!ramo.StartsWith(Prefisso, StringComparison.Ordinal))
            return $"{ramo} non è un ramo di prova: il Lab butta solo i prova/….";
        if (await RamiDelSector.BaseDiAsync(git, radice, ramo, annulla).ConfigureAwait(false) is not { } base_)
            return $"Non so da che ramo è partita {ramo}: passa a mano al ramo di lavoro, poi cancellalo.";
        var passa = await git.EseguiAsync(radice, ["switch", "--quiet", base_], annulla).ConfigureAwait(false);
        if (!passa.Riuscito)
            return $"git non è tornato a {base_}: {passa.Errore}";
        var cancella = await git.EseguiAsync(radice, ["branch", "--quiet", "-D", ramo], annulla).ConfigureAwait(false);
        return cancella.Riuscito ? null : $"Tornato a {base_}, ma il ramo {ramo} non si è cancellato: {cancella.Errore}";
    }
}

/// <summary>
/// La prova B9 della carta «file per file» (§2): l'ordine a blocchi delle aerovie. Oggi <c>itawlow.lairway</c> ha prima
/// tutti i tracciati (<c>T</c>) e poi tutte le etichette (<c>L</c>); il blocco per aerovia (B1) mette insieme, fra
/// <c>//@"NOME"</c>, <c>//@START</c> e <c>//@END "NOME"</c>, le sue etichette e poi il suo tracciato. Prima di riscrivere
/// il file vero si guarda in Aurora che si veda tutto come prima.
/// </summary>
/// <remarks>
/// La variante sposta le righe, non le cambia: ogni riga <c>T</c> e <c>L</c> c'è una volta, uguale. Restano com'erano le
/// righe prima del primo dato (l'intestazione di KY139) e quelle dopo l'ultimo (la lista <c>//Waypoint references</c>,
/// che si toglie a parte, B10). Fra i dati, i commenti di sezione (<c>//Airway tracks</c>, <c>//Airway labels</c>) e
/// le righe vuote si tolgono; un <c>BREAK</c> resta dov'era, fra i due pezzi che spezza. Un'etichetta condivisa
/// (<c>L613-L615</c>) va nel blocco della prima in ordine alfabetico (B4).
/// </remarks>
public sealed class OrdineABlocchiDelleAerovie : ProvaInAurora
{
    /// <summary>Il file delle aerovie di bassa, dalla radice del repo.</summary>
    public const string File = "SectorFiles/Include/IT/AIRWAY/itawlow.lairway";

    public override string Argomento => "aerovie-a-blocchi";

    public override string Titolo => "l'ordine a blocchi delle aerovie (B9)";

    public override string Perche
        => "«File per file» B9: prima di riscrivere itawlow.lairway un blocco per aerovia (etichette poi tracciato), si guarda in Aurora che si veda tutto come prima.";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string percorso = Path.Combine(radice, File);
        if (!System.IO.File.Exists(percorso))
        {
            perche = $"Nel sector non c'è {File}.";
            return null;
        }

        string testo = System.IO.File.ReadAllText(percorso, Encoding.Latin1);
        var esito = Riordina(testo, out perche);
        if (esito is null)
            return null;
        var (nuovo, conti) = esito.Value;
        perche = null;
        var domande = new List<DomandaDellaProva>
        {
            new($"Le aerovie basse si vedono tutte, come prima ({conti.Aerovie} aerovie)?", "le aerovie basse: tutte accese, confronta col sector pubblicato"),
            new($"Le etichette stanno dove stavano, a metà dei tratti, nessuna in più o in meno ({conti.Etichette})?", "le aerovie basse, ingrandendo su una zona fitta"),
        };
        // In Aurora un'aerovia non si cerca per nome (committente, 18 ottobre): le domande dicono i fix dove guardare.
        if (conti.ConBreak is { } spezzata)
            domande.Add(new($"{spezzata} resta spezzata dove c'è il BREAK ({conti.Break} interruzioni nel file)?",
                $"fra {string.Join(" e fra ", conti.BreakFra)}: lì la linea di {spezzata} non deve esserci"));
        if (conti.Condivisa is { } condivisa)
            domande.Add(new($"Un'etichetta condivisa ({condivisa}) si vede una volta sola, come prima?",
                $"il tratto comune di {condivisa.Replace("-", " e ", StringComparison.Ordinal)}{(conti.CondivisaFra.Count > 0 ? ", " + string.Join("–", conti.CondivisaFra) : "")}"));
        domande.Add(new("Il sector si carica senza errori nuovi per le righe //@ dei blocchi?", "i messaggi di Aurora al caricamento del sector"));
        var fatto = new List<string>
        {
            $"{File}: {conti.Aerovie} aerovie in blocchi //@\"NOME\" … //@END, ognuno con le sue etichette e poi il tracciato.",
            $"Righe T e L spostate, non cambiate: {conti.Tracciati} T, {conti.Etichette} L, {conti.Break} BREAK; tolte {conti.Tolte} righe di commento o vuote fra i dati.",
        };
        return new ProvaPreparata([new FileDellaProva(File, nuovo)], domande, fatto);
    }

    /// <summary>Quanti dati ha spostato la variante, e gli esempi per le domande.</summary>
    public sealed record Conti(int Aerovie, int Tracciati, int Etichette, int Break, int Tolte, string? ConBreak, string? Condivisa)
    {
        /// <summary>Dove si spezza <see cref="ConBreak"/>: «GEMVI e MATED», un elemento per BREAK (i fix si trovano in Aurora).</summary>
        public IReadOnlyList<string> BreakFra { get; init; } = [];

        /// <summary>I fix del tratto comune delle aerovie di <see cref="Condivisa"/>, in ordine.</summary>
        public IReadOnlyList<string> CondivisaFra { get; init; } = [];
    }

    /// <summary>Il testo a blocchi, o null col perché (un caso che la prova non sa rimettere senza cambiare il disegno).</summary>
    public static (string Testo, Conti Conti)? Riordina(string testo, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(testo);
        string acapo = testo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var righe = testo.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        bool finisceAcapo = righe.Count > 0 && righe[^1].Length == 0;
        if (finisceAcapo)
            righe.RemoveAt(righe.Count - 1);

        int primo = righe.FindIndex(Dato), ultimo = righe.FindLastIndex(Dato);
        if (primo < 0)
        {
            perche = "Nel file non ci sono righe T o L.";
            return null;
        }

        var ordine = new List<string>();
        var tracciati = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var dichiarazioni = new Dictionary<string, string>(StringComparer.Ordinal);
        var etichette = new List<string>();
        var inAttesa = new List<string>();
        string? ultima = null;
        int tolte = 0, quantiBreak = 0, quantiT = 0;
        var spezzate = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = primo; i <= ultimo; i++)
        {
            string riga = righe[i], pulita = riga.Trim();
            if (pulita.StartsWith("L;", StringComparison.Ordinal))
            {
                etichette.Add(riga);
                continue;
            }

            if (pulita.StartsWith("//@@", StringComparison.Ordinal) || CampoDue(pulita) == "BREAK" || pulita.Length == 0)
            {
                inAttesa.Add(riga);
                continue;
            }

            if (pulita.StartsWith("//@\"", StringComparison.Ordinal))
            {
                if (Nome(pulita) is { } n)
                    dichiarazioni[n] = riga;
                continue;
            }

            if (!pulita.StartsWith("T;", StringComparison.Ordinal))
            {
                // Commenti di sezione, //@START, //@END: i blocchi li riscrive la variante.
                if (!pulita.StartsWith("//@", StringComparison.Ordinal))
                    tolte++;
                continue;
            }

            string aerovia = CampoDue(pulita)!;
            quantiT++;
            var interruzioni = inAttesa.Where(r => CampoDue(r.Trim()) == "BREAK").ToList();
            var tag = inAttesa.Where(r => r.Trim().StartsWith("//@@", StringComparison.Ordinal)).ToList();
            tolte += inAttesa.Count(r => r.Trim().Length == 0);
            if (!tracciati.TryGetValue(aerovia, out var suo))
            {
                tracciati[aerovia] = suo = [];
                ordine.Add(aerovia);
                // Un BREAK fra due aerovie diverse è dell'aerovia prima: resta in coda a lei.
                if (ultima is not null)
                    tracciati[ultima].AddRange(interruzioni);
            }
            else if (aerovia != ultima)
            {
                // Sul fork non capita: i pezzi di un'aerovia sono tutti di seguito. Metterli insieme cambierebbe il disegno.
                perche = $"L'aerovia {aerovia} torna più avanti nel file, dopo altre aerovie (riga {i + 1}): metterne i pezzi di seguito potrebbe unirli, e la prova non lo sa rimettere.";
                return null;
            }
            else
            {
                suo.AddRange(interruzioni);
                if (interruzioni.Count > 0)
                    spezzate[aerovia] = spezzate.GetValueOrDefault(aerovia) + interruzioni.Count;
            }

            quantiBreak += interruzioni.Count;
            suo.AddRange(tag);
            suo.Add(riga);
            inAttesa.Clear();
            ultima = aerovia;
        }

        // Un BREAK (o un tag) dopo l'ultimo punto, prima delle etichette, è in coda all'ultima aerovia.
        if (ultima is not null)
        {
            tracciati[ultima].AddRange(inAttesa.Where(r => r.Trim().Length > 0));
            quantiBreak += inAttesa.Count(r => CampoDue(r.Trim()) == "BREAK");
            tolte += inAttesa.Count(r => r.Trim().Length == 0);
        }

        // L'etichetta va nel blocco della prima, in ordine alfabetico, fra le aerovie che nomina e che ci sono.
        var perAerovia = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var orfane = new List<string>();
        foreach (string etichetta in etichette)
        {
            var nomi = (CampoDue(etichetta.Trim()) ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (nomi.Where(tracciati.ContainsKey).Order(StringComparer.Ordinal).FirstOrDefault() is { } prima)
                (perAerovia.TryGetValue(prima, out var l) ? l : perAerovia[prima] = []).Add(etichetta);
            else
                orfane.Add(etichetta);
        }

        var uscita = new List<string>(righe.Take(primo));
        foreach (string aerovia in ordine)
        {
            uscita.Add(dichiarazioni.GetValueOrDefault(aerovia) ?? $"//@\"{aerovia}\"");
            uscita.Add("//@START");
            uscita.AddRange(perAerovia.GetValueOrDefault(aerovia) ?? []);
            uscita.AddRange(tracciati[aerovia]);
            uscita.Add($"//@END \"{aerovia}\"");
            uscita.Add("");
        }

        if (orfane.Count > 0)
        {
            uscita.Add("//Etichette senza aerovia");
            uscita.AddRange(orfane);
            uscita.Add("");
        }

        uscita.AddRange(righe.Skip(ultimo + 1));
        perche = null;
        // L'esempio di condivisa per la domanda: una i cui nomi ci sono tutti (non «UA145-A145», con la U che non esiste più).
        string condivisa = etichette.Select(e => CampoDue(e.Trim()))
            .FirstOrDefault(n => n is not null && n.Contains('-', StringComparison.Ordinal) && n.Split('-').All(tracciati.ContainsKey)) ?? "";
        string? conBreak = spezzate.OrderByDescending(s => s.Value).Select(s => s.Key).FirstOrDefault();
        return (string.Join(acapo, uscita) + (finisceAcapo ? acapo : ""),
            new Conti(ordine.Count, quantiT, etichette.Count, quantiBreak, tolte, conBreak, condivisa.Length > 0 ? condivisa : null)
            {
                BreakFra = conBreak is null ? [] : DoveSiSpezza(tracciati[conBreak]),
                CondivisaFra = condivisa.Length > 0 ? TrattoComune(condivisa.Split('-'), tracciati) : [],
            });
    }

    /// <summary>Per ogni BREAK del tracciato: l'ultimo punto prima e il primo dopo («GEMVI e MATED»).</summary>
    private static List<string> DoveSiSpezza(List<string> righe)
    {
        var dove = new List<string>();
        var punti = righe.Select(r => r.Trim()).Where(r => r.StartsWith("T;", StringComparison.Ordinal)).Select(r => r.Split(';')).ToList();
        for (int i = 1; i < punti.Count - 1; i++)
        {
            if (punti[i][1].Trim() == "BREAK" && punti[i - 1][1].Trim() != "BREAK" && punti[i + 1][1].Trim() != "BREAK")
                dove.Add($"{punti[i - 1][2].Trim()} e {punti[i + 1][2].Trim()}");
        }

        return dove;
    }

    /// <summary>I fix dei tratti che tutte le aerovie nominate hanno in comune, nell'ordine della prima.</summary>
    private static List<string> TrattoComune(string[] nomi, Dictionary<string, List<string>> tracciati)
    {
        List<string> Punti(string aerovia) => [.. tracciati[aerovia].Select(r => r.Trim()).Where(r => r.StartsWith("T;", StringComparison.Ordinal))
            .Select(r => r.Split(';')).Where(c => c[1].Trim() != "BREAK").Select(c => c[2].Trim())];
        var coppie = nomi.Select(n => Punti(n)).Select(p => p.Zip(p.Skip(1), (a, b) => (a, b)).SelectMany(t => new[] { t, (t.b, t.a) }).ToHashSet()).ToList();
        var prima = Punti(nomi[0]);
        var comuni = new List<string>();
        for (int i = 0; i + 1 < prima.Count; i++)
        {
            if (coppie.All(c => c.Contains((prima[i], prima[i + 1]))))
            {
                if (comuni.Count == 0 || comuni[^1] != prima[i])
                    comuni.Add(prima[i]);
                comuni.Add(prima[i + 1]);
            }
        }

        return comuni;
    }

    private static bool Dato(string riga)
    {
        string pulita = riga.Trim();
        return pulita.StartsWith("T;", StringComparison.Ordinal) || pulita.StartsWith("L;", StringComparison.Ordinal);
    }

    /// <summary>Il secondo campo di una riga <c>T;</c>/<c>L;</c> (il nome dell'aerovia), o null.</summary>
    private static string? CampoDue(string pulita)
        => pulita.Length > 2 && pulita[1] == ';' && pulita.Split(';') is { Length: > 1 } campi ? campi[1].Trim() : null;

    /// <summary>Il nome di una dichiarazione <c>//@"NOME" …</c>, o null.</summary>
    private static string? Nome(string pulita)
    {
        int fine = pulita.IndexOf('"', 4);
        return fine > 4 ? pulita[4..fine] : null;
    }
}
