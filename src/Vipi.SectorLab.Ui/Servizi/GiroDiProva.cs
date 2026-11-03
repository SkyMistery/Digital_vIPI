using System.Globalization;
using System.Text;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>Come è andato un compito del Giro di prova (carta «Il kit di prova per gli AOD», K6).</summary>
public enum EsitoDelCompito
{
    Va,
    NonVa,
    /// <summary>K6: non un «non va», il segnale che il Lab lì non si spiega da solo.</summary>
    NonHoCapito,
}

/// <summary>
/// Un compito del giro: dove si fa (<paramref name="Pagina"/>, null se non ha una pagina), che cosa fare, che cosa si
/// dovrebbe vedere. <paramref name="SoloNota"/>: il compito è una domanda, si risponde con la nota e basta.
/// </summary>
public sealed record CompitoDelGiro(string Id, string Titolo, PaginaDelLab? Pagina, string Fai, string DovrestiVedere, bool SoloNota = false);

/// <summary>L'esito dato a un compito, con la nota e quando. <paramref name="Esito"/> è null per un compito <c>SoloNota</c>.</summary>
public sealed record EsitoDelGiro(string Compito, EsitoDelCompito? Esito, string? Nota, DateTime Quando);

/// <summary>
/// Un giro di prova (carta «I giri di prova dal fork», P3): i suoi compiti, la versione (più alta = cambiato: P4), per chi
/// è. <paramref name="File"/> è il file del fork da cui viene (<c>02-ground.json</c>), null per quello dentro il Lab (P8).
/// </summary>
public sealed record Giro(string Id, string Titolo, int Versione, string? PerChi, IReadOnlyList<CompitoDelGiro> Compiti, string? File);

/// <summary>
/// I giri di prova del kit per gli AOD (carte «Il kit di prova per gli AOD» e «I giri di prova dal fork», 3 novembre
/// 2026): «Primi passi», scritto per chi non ha mai visto il Lab, il lettore dei giri del fork, e il testo della issue che
/// manda gli esiti al committente (K8).
/// </summary>
public static class GiroDiProva
{
    /// <summary>K8: oltre questi caratteri (già scritti per il link) il testo della issue si taglia.</summary>
    public const int TettoDelTesto = 6000;

    /// <summary>K8: quanti errori del registro vanno nella issue, al massimo.</summary>
    public const int ErroriNellIssue = 15;

    /// <summary>Il file con gli esiti interi, nei dati del Lab: si scrive a ogni «Manda gli esiti».</summary>
    public const string FileDegliEsiti = "esiti-del-giro.md";

    /// <summary>I compiti di «Primi passi», nell'ordine in cui si fanno (carta del kit, §4, G1-G12).</summary>
    private static readonly IReadOnlyList<CompitoDelGiro> CompitiDeiPrimiPassi =
    [
        new("G1", "Chi sei e che Aurora usi", PaginaDelLab.Impostazioni,
            "Scrivi il tuo VID IVAO. Poi scegli il profilo di Aurora che usi di solito.",
            "Il VID resta scritto; tornando alla Mappa, i punti hanno i simboli del tuo profilo."),
        new("G2", "Un record del sector", PaginaDelLab.Sector,
            "Nella colonna di sinistra apri una cartella, poi un file, poi un record.",
            "A destra la Scheda del record, per gruppi (Dati, Geometria, Collegamenti…); sulla mappa la sua forma accesa."),
        new("G3", "Lavorare su uno scalo", PaginaDelLab.Sector,
            "Nella colonna di sinistra la linguetta Scali: scegli il tuo scalo, poi «◎ Lavora su …».",
            "La mappa va sul sedime e mostra solo lo scalo; la colonna resta sullo scalo finché non premi «esci»."),
        new("G4", "Modificare sulla mappa", PaginaDelLab.Sector,
            "Sempre sullo scalo: scegli uno stand o una taxiway, nella Scheda «✎ Modifica sulla mappa», trascina un vertice o lo stand.",
            "La forma si sposta dove la lasci; nella barra in alto «Salva (1)»."),
        new("G5", "Guardare e annullare", PaginaDelLab.Consegna,
            "In Consegna guarda la modifica appena fatta, poi «Annulla».",
            "Le righe del file prima e dopo; annullata, la barra torna a «Salva» senza numero."),
        new("G6", "Lavorare su un settore", PaginaDelLab.Sector,
            "Esci dallo scalo; linguetta Settori: scegli una posizione, «◎ Lavora su …»; poi «esci».",
            "Il settore sulla mappa e, in grigio, quelli accanto; uscito, di nuovo il sector intero."),
        // Su un ambiente nuovo l'Indice chiede prima il ciclo (26 ottobre: il ciclo lo sceglie l'AOD): visto da zero sul banco.
        new("G7", "L'AIP espanso", PaginaDelLab.Aip,
            "La prima volta scegli il ciclo: qui, sulla riga del ciclo in vigore, «Lavora su questo». Poi apri l'Indice (in alto, AIP espanso) e cerca una voce del tuo scalo: una SID, un punto.",
            "La scheda della voce com'è nell'AIP, e la sua forma sulla mappa."),
        new("G8", "Confrontare l'AIP col sector", PaginaDelLab.Confronti,
            "Apri la sezione «Punti significativi»: guarda uguali, diverse e mancanti; apri una riga diversa.",
            "I due lati sulla mappa, arancio l'AIP e blu il sector, e i tasti per portare l'AIP nel sector."),
        new("G9", "Il testo di un file", PaginaDelLab.Testo,
            "Apri un file, cambia una riga, «Applica». Poi in Consegna annulla la modifica.",
            "La modifica in sospeso come quelle fatte dalla Scheda."),
        new("G10", "I problemi del sector", PaginaDelLab.Sector,
            "In alto a destra il tasto «N errori · M avvisi»: aprilo e scegline uno.",
            "La spiegazione del problema e il record che lo dà, scelto sulla mappa."),
        new("G11", "Mandare una modifica", PaginaDelLab.Consegna,
            "Una modifica vera e piccola sul tuo scalo: «Salva», «Registra il sector», «Manda». Va sul repo di prova, non su quello vero.",
            "Si apre GitHub con la PR già scritta: premi «Create pull request»."),
        new("G12", "Che cosa ti è mancato?", null,
            "Che cosa non hai trovato, che cosa ti serve che il Lab non fa, che cosa cambieresti.",
            "Scrivilo qui sotto e premi «Scrivi la nota».", SoloNota: true),
    ];

    /// <summary>L'id di «Primi passi»: anche il giro di prima della carta dei giri, quando ce n'era uno solo.</summary>
    public const string IdDeiPrimiPassi = "primi-passi";

    /// <summary>
    /// P8: «Primi passi» dentro il Lab, il ripiego se il fork non ha giri o non risponde. Un <c>primi-passi.json</c> sul
    /// fork prende il suo posto.
    /// </summary>
    public static Giro PrimiPassi { get; } = new(IdDeiPrimiPassi, "Primi passi", 1, "Tutti, per primo", CompitiDeiPrimiPassi, null);

    /// <summary>
    /// P3: un giro dal testo di un suo file. Null, col perché, se non si legge: JSON rotto, senza <c>id</c> (lettere minuscole,
    /// cifre e «-») o <c>titolo</c>, senza compiti, un compito senza <c>id</c>, <c>titolo</c> o <c>fai</c>, due compiti con lo
    /// stesso <c>id</c>. Un campo sconosciuto si ignora (un giro scritto per un Lab più nuovo si legge lo stesso), una
    /// <c>pagina</c> sconosciuta vale «nessuna pagina».
    /// </summary>
    public static Giro? Leggi(string file, string testo, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(testo);
        perche = null;
        try
        {
            using var documento = System.Text.Json.JsonDocument.Parse(testo, new System.Text.Json.JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            });
            var radice = documento.RootElement;
            if (radice.ValueKind != System.Text.Json.JsonValueKind.Object)
                return Rifiuta("non è un oggetto { … }", out perche);
            if (Testo(radice, "id") is not { } id || !id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
                return Rifiuta("manca «id», o non è fatto di lettere minuscole, cifre e «-»", out perche);
            if (Testo(radice, "titolo") is not { } titolo)
                return Rifiuta("manca «titolo»", out perche);
            int versione = radice.TryGetProperty("versione", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number
                           && v.TryGetInt32(out int numero) && numero > 0 ? numero : 1;
            if (!radice.TryGetProperty("compiti", out var elenco) || elenco.ValueKind != System.Text.Json.JsonValueKind.Array
                || elenco.GetArrayLength() == 0)
                return Rifiuta("manca «compiti», o è vuoto", out perche);

            var compiti = new List<CompitoDelGiro>();
            int posto = 0;
            foreach (var compito in elenco.EnumerateArray())
            {
                posto++;
                if (compito.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return Rifiuta($"il compito {posto} non è un oggetto {{ … }}", out perche);
                if (Testo(compito, "id") is not { } suo)
                    return Rifiuta($"il compito {posto} non ha «id»", out perche);
                if (compiti.Any(c => string.Equals(c.Id, suo, StringComparison.Ordinal)))
                    return Rifiuta($"due compiti con l'id «{suo}»", out perche);
                if (Testo(compito, "titolo") is not { } nome || Testo(compito, "fai") is not { } fai)
                    return Rifiuta($"il compito «{suo}» non ha «titolo» o «fai»", out perche);
                bool soloNota = compito.TryGetProperty("soloNota", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.True;
                compiti.Add(new CompitoDelGiro(suo, nome, PaginaDaNome(Testo(compito, "pagina")), fai, Testo(compito, "dovresti") ?? "", soloNota));
            }

            return new Giro(id, titolo, versione, Testo(radice, "perChi"), compiti, file);
        }
        catch (System.Text.Json.JsonException e)
        {
            return Rifiuta($"non è un JSON valido ({e.Message})", out perche);
        }
    }

    /// <summary>La pagina dal suo nome nel file del giro (<c>Sector</c>, <c>AipIndice</c>…), null se non c'è o non si conosce.</summary>
    public static PaginaDelLab? PaginaDaNome(string? nome) =>
        nome is not null && Enum.TryParse<PaginaDelLab>(nome, ignoreCase: false, out var pagina) && Enum.IsDefined(pagina)
            && !nome.Any(char.IsAsciiDigit)
            ? pagina
            : null;

    private static Giro? Rifiuta(string perche, out string? dove)
    {
        dove = perche;
        return null;
    }

    private static string? Testo(System.Text.Json.JsonElement oggetto, string nome) =>
        oggetto.TryGetProperty(nome, out var valore) && valore.ValueKind == System.Text.Json.JsonValueKind.String
        && valore.GetString() is { } testo && !string.IsNullOrWhiteSpace(testo)
            ? testo.Trim()
            : null;

    /// <summary>Il nome della pagina come lo dice la barra: «Sector · Mappa», «AIP espanso · Indice»…</summary>
    public static string NomeDellaPagina(PaginaDelLab pagina) => pagina switch
    {
        PaginaDelLab.Sector => "Sector · Mappa",
        PaginaDelLab.Testo => "Sector · Testo",
        PaginaDelLab.Consegna => "Sector · Consegna",
        PaginaDelLab.AipIndice => "AIP espanso · Indice",
        PaginaDelLab.Aip => "AIP espanso · Lettura e consegna",
        PaginaDelLab.Confronti => "Confronti",
        PaginaDelLab.Impostazioni => "Impostazioni",
        _ => pagina.ToString(),
    };

    /// <summary>Vero se il compito ha la sua risposta: un esito, o la nota per un compito <c>SoloNota</c>.</summary>
    public static bool Fatto(CompitoDelGiro compito, EsitoDelGiro? esito) =>
        esito is not null && (compito.SoloNota ? !string.IsNullOrWhiteSpace(esito.Nota) : esito.Esito is not null);

    /// <summary>Il segno dell'esito, nei pallini e nella issue.</summary>
    public static string Segno(EsitoDelCompito? esito) => esito switch
    {
        EsitoDelCompito.Va => "✅",
        EsitoDelCompito.NonVa => "❌",
        EsitoDelCompito.NonHoCapito => "❓",
        _ => "·",
    };

    /// <summary>
    /// Il titolo e il testo intero della issue (K8, P6): il giro, chi, una riga per compito, gli errori del registro.
    /// </summary>
    public static (string Titolo, string Testo) Issue(Giro giro, string vid, string versione, string sistema, DateTime quando,
        IReadOnlyDictionary<string, EsitoDelGiro> esiti, IReadOnlyList<string> errori)
    {
        ArgumentNullException.ThrowIfNull(giro);
        ArgumentNullException.ThrowIfNull(esiti);
        ArgumentNullException.ThrowIfNull(errori);
        string giorno = quando.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var testo = new StringBuilder();
        testo.Append(CultureInfo.InvariantCulture,
            $"**Giro** {giro.Titolo} v{giro.Versione} ({(giro.File is { } file ? $"`{GiriDalServerCartella}/{file}`" : "dentro il Lab")})\n");
        testo.Append(CultureInfo.InvariantCulture, $"**VID** {vid} · **Lab** {versione} · {sistema} · {quando:dd/MM/yyyy HH:mm}\n\n");

        int fatti = giro.Compiti.Count(c => Fatto(c, esiti.GetValueOrDefault(c.Id)));
        testo.Append(CultureInfo.InvariantCulture, $"{fatti} compiti su {giro.Compiti.Count}: ")
            .Append(CultureInfo.InvariantCulture, $"✅ {Conta(giro, esiti, EsitoDelCompito.Va)} · ❌ {Conta(giro, esiti, EsitoDelCompito.NonVa)} · ❓ {Conta(giro, esiti, EsitoDelCompito.NonHoCapito)}\n\n");
        testo.Append("| | Compito | Nota |\n|---|---|---|\n");
        foreach (var compito in giro.Compiti)
        {
            var esito = esiti.GetValueOrDefault(compito.Id);
            string segno = compito.SoloNota ? (Fatto(compito, esito) ? "📝" : "·") : Segno(esito?.Esito);
            testo.Append(CultureInfo.InvariantCulture, $"| {segno} | {compito.Id} {Cella(compito.Titolo)} | {Cella(esito?.Nota)} |\n");
        }

        testo.Append("\n**Errori del registro**");
        if (errori.Count == 0)
        {
            testo.Append(": nessuno.\n");
        }
        else
        {
            testo.Append(CultureInfo.InvariantCulture, $" (gli ultimi {errori.Count}):\n\n```\n");
            foreach (string errore in errori)
                testo.Append(errore.Replace("```", "'''", StringComparison.Ordinal)).Append('\n');
            testo.Append("```\n");
        }

        return ($"Giro «{giro.Titolo}» v{giro.Versione} · VID {vid} · {giorno}", testo.ToString());
    }

    private const string GiriDalServerCartella = Core.Git.GiriDalServer.Cartella;

    /// <summary>
    /// La pagina di GitHub «nuova issue» già compilata (K8), sul repo <c>owner/repo</c>. Se il testo non sta nel link
    /// (<see cref="TettoDelTesto"/>) si taglia a una riga intera e dice dove sta il file intero: <c>Tagliato</c> è vero.
    /// </summary>
    public static (Uri Pagina, bool Tagliato) PaginaDellaIssue(string repo, string titolo, string testo, string fileIntero)
    {
        ArgumentException.ThrowIfNullOrEmpty(repo);
        ArgumentNullException.ThrowIfNull(testo);
        string corpo = testo;
        bool tagliato = false;
        if (Uri.EscapeDataString(testo).Length > TettoDelTesto)
        {
            tagliato = true;
            string coda = $"\n…\n\n**Il testo intero è in `{fileIntero}`: trascina il file qui.**\n";
            int tetto = TettoDelTesto - Uri.EscapeDataString(coda).Length;
            var righe = testo.Split('\n');
            var tenuto = new StringBuilder();
            foreach (string riga in righe)
            {
                if (Uri.EscapeDataString(tenuto + riga + "\n").Length > tetto)
                    break;
                tenuto.Append(riga).Append('\n');
            }
            // Un blocco di codice aperto e tagliato si chiude, se no la coda finisce dentro.
            if (tenuto.ToString().Split("```").Length % 2 == 0)
                tenuto.Append("```\n");
            corpo = tenuto + coda;
        }

        var pagina = new Uri($"https://github.com/{repo}/issues/new?title={Uri.EscapeDataString(titolo)}&body={Uri.EscapeDataString(corpo)}");
        return (pagina, tagliato);
    }

    /// <summary>
    /// K8: gli ultimi errori del registro (le righe <c>ERRORE</c>, la prima riga di ognuna, dai file più nuovi), al massimo
    /// <see cref="ErroriNellIssue"/>, ciascuno a 300 caratteri.
    /// </summary>
    public static IReadOnlyList<string> ErroriDelRegistro(string cartella)
    {
        if (!Directory.Exists(cartella))
            return [];
        var errori = new List<string>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(cartella, "lab-*.txt").OrderBy(f => f, StringComparer.Ordinal))
            {
                using var flusso = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var lettore = new StreamReader(flusso);
                while (lettore.ReadLine() is { } riga)
                {
                    // «aaaa-mm-gg hh:mm:ss.fff [filo] ERRORE      …»: le righe dopo sono lo stack della stessa eccezione.
                    int chiusa = riga.IndexOf("] ", StringComparison.Ordinal);
                    if (chiusa > 0 && riga.Length > 4 && char.IsAsciiDigit(riga[0])
                        && riga.AsSpan(chiusa + 2).StartsWith("ERRORE ", StringComparison.Ordinal))
                        errori.Add(riga.Length > 300 ? riga[..300] + "…" : riga);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Un file del registro che non si legge: si manda quel che si è letto.
        }

        return errori.Count > ErroriNellIssue ? errori.GetRange(errori.Count - ErroriNellIssue, ErroriNellIssue) : errori;
    }

    private static int Conta(Giro giro, IReadOnlyDictionary<string, EsitoDelGiro> esiti, EsitoDelCompito quale) =>
        giro.Compiti.Count(c => !c.SoloNota && esiti.GetValueOrDefault(c.Id)?.Esito == quale);

    /// <summary>Una cella della tabella markdown: niente «|» e niente a capo.</summary>
    private static string Cella(string? testo) => string.IsNullOrWhiteSpace(testo)
        ? ""
        : testo.Trim().Replace("|", "\\|", StringComparison.Ordinal).Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
}
