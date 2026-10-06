using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Sectorfile;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorfileProva;

/// <summary>
/// La prova di concordanza fra il motore nuovo (<c>Vipi.Sectorfile</c>) e il lettore di produzione di vIPI
/// (<see cref="AuroraSectorfileParser"/>), su UN file (carta F2 §2.4, slice 9). Due lettori dello stesso formato
/// non possono dire due cose diverse in silenzio: o dicono la stessa, o la differenza si elenca e si spiega.
/// </summary>
/// <remarks>
/// <para>Si confronta solo ciò che tutti e due leggono. Il <c>Fix</c> completato delle SID di vIPI (alias,
/// <c>NeedsFixReview</c>) il motore non lo ha, e il motore ha cose (tracciati, attese, forme) che vIPI non legge.</para>
/// <para>Il file si passa a vIPI come lo riceve in produzione: testo UTF-8 (raw.githubusercontent), diviso sugli
/// <c>\n</c>. Il motore lo legge col suo lettore (UTF-8 stretto, ripiego Windows-1252).</para>
/// <para>⚠️ Un file solo, compilato in due posti: nello strumento (l'albero intero) e in
/// <c>Vipi.Infrastructure.Tests</c> (i campioni, in CI). Due copie della regola sarebbero due verità.</para>
/// </remarks>
public static class Concordanza
{
    /// <summary>Scarto massimo fra due coordinate dello stesso punto: un decimillesimo di secondo d'arco, come la
    /// concordanza del DMS (misura 4).</summary>
    private const double Scarto = 1e-4 / 3600;

    /// <summary>L'esito su un file.</summary>
    /// <param name="Concordi">Oggetti letti uguali dai due.</param>
    /// <param name="Discordi">Stesso oggetto (stesso nome) letto con valori diversi.</param>
    /// <param name="SoloVipi">Oggetti che vIPI legge e il motore no (o vIPI legge senza coordinate).</param>
    /// <param name="SoloMotore">Oggetti che il motore legge e vIPI no.</param>
    /// <param name="RifiutatiDaEntrambi">Righe che nessuno dei due legge: concordi nel rifiuto, sono errori del
    /// sector (il validatore li elenca: <c>KPT</c>, <c>PL-BRAVO</c>, <c>MG763</c>).</param>
    public sealed record Esito(
        int Concordi,
        IReadOnlyList<string> Discordi,
        IReadOnlyList<string> SoloVipi,
        IReadOnlyList<string> SoloMotore,
        IReadOnlyList<string> RifiutatiDaEntrambi)
    {
        public bool Pulito => Discordi.Count == 0 && SoloVipi.Count == 0 && SoloMotore.Count == 0;
    }

    /// <summary>
    /// I punti di un file <c>.fix</c>/<c>.vor</c>/<c>.ndb</c>: stesso nome e stesse coordinate. Gli omonimi nello
    /// stesso file (Grosseto: VOR e TACAN) si appaiano uno a uno, il più vicino prima.
    /// </summary>
    public static Esito DeiPunti(string percorso, NavaidKind natura)
    {
        var diVipi = AuroraSectorfileParser.ParseNavaids(new[] { (natura, (string?)TestoDiProduzione(percorso)) }).Righe;

        var avvisi = new Silenzio();
        var palette = new ColorPalette();
        List<(string Nome, Coordinate Posizione, int Riga)> delMotore = natura switch
        {
            NavaidKind.Vor => new VorParser(avvisi).Parse(percorso, palette).Records
                .Select(v => (v.Ident, v.Position, v.Source.LineNumber)).ToList(),
            NavaidKind.Ndb => new NdbParser(avvisi).Parse(percorso, palette).Records
                .Select(n => (n.Ident, n.Position, n.Source.LineNumber)).ToList(),
            _ => new FixParser(avvisi).Parse(percorso, palette).Records
                .Select(f => (f.Name, f.Position, f.Source.LineNumber)).ToList(),
        };

        int concordi = 0;
        var discordi = new List<string>();
        var soloVipi = new List<string>();
        var rifiutati = new List<string>();
        var liberi = delMotore.ToList();

        foreach (var punto in diVipi)
        {
            var omonimi = liberi.Where(m => string.Equals(m.Nome, punto.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (punto.Lat is not { } lat || punto.Lon is not { } lon)
            {
                // vIPI tiene il nome senza posizione (nessuna coppia DMS nella riga): per chi disegna è un punto che
                // non c'è. Se il motore lo legge, è una lettura che vIPI perde; se no, tutti e due la rifiutano.
                if (omonimi.Count > 0)
                {
                    // Un oggetto solo, una differenza sola: il punto del motore non resta fra i «solo motore».
                    liberi.Remove(omonimi[0]);
                    soloVipi.Add($"{punto.Name}: vIPI senza coordinate, il motore le legge (riga {omonimi[0].Riga})");
                }
                else
                {
                    rifiutati.Add(punto.Name);
                }

                continue;
            }

            if (omonimi.Count == 0)
            {
                soloVipi.Add($"{punto.Name} {Scrivi(lat, lon)}");
                continue;
            }

            var vicino = omonimi.MinBy(m => Math.Abs(m.Posizione.LatitudeDeg - lat) + Math.Abs(m.Posizione.LongitudeDeg - lon));
            liberi.Remove(vicino);
            if (Math.Abs(vicino.Posizione.LatitudeDeg - lat) <= Scarto && Math.Abs(vicino.Posizione.LongitudeDeg - lon) <= Scarto)
            {
                concordi++;
            }
            else
            {
                discordi.Add($"{punto.Name} (riga {vicino.Riga}): vIPI {Scrivi(lat, lon)}, motore {Scrivi(vicino.Posizione.LatitudeDeg, vicino.Posizione.LongitudeDeg)}");
            }
        }

        return new Esito(concordi, discordi, soloVipi,
            liberi.Select(m => $"{m.Nome} (riga {m.Riga}) {Scrivi(m.Posizione.LatitudeDeg, m.Posizione.LongitudeDeg)}").ToList(),
            rifiutati);
    }

    /// <summary>
    /// Le SID di un <c>&lt;icao&gt;.sid</c> o le STAR di un <c>&lt;icao&gt;.str</c>: l'insieme (nome, pista), una
    /// voce per pista come la produce vIPI. Al motore si applicano GLI STESSI filtri che vIPI applica: la riga
    /// comincia con l'ICAO del file; per le STAR, tipo vuoto o 0 e almeno una pista di forma pista.
    /// </summary>
    public static Esito DelleProcedure(string percorso, bool star)
    {
        string icao = Path.GetFileNameWithoutExtension(percorso).ToUpperInvariant();
        string testo = TestoDiProduzione(percorso);
        var nessunNome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nessunAlias = new Dictionary<string, string>();
        var diVipi = (star
                ? AuroraSectorfileParser.ParseStars(icao, testo, nessunNome, nessunAlias)
                : AuroraSectorfileParser.ParseSids(icao, testo, nessunNome, nessunAlias))
            .Select(p => Chiave(p.Name, p.Runway))
            .ToList();

        var avvisi = new Silenzio();
        var palette = new ColorPalette();
        var delMotore = new List<string>();
        if (star)
        {
            foreach (var r in new StrParser(avvisi).Parse(percorso, palette).Records)
            {
                if (!string.Equals(r.IcaoCode, icao, StringComparison.OrdinalIgnoreCase) || r.ProcedureId.Length == 0
                    || r.RecordType != StrRecordType.Star)
                {
                    continue;
                }

                delMotore.AddRange(Piste(r.RunwaySpec).Where(p => p is not null && FormaPista.IsMatch(p.ToUpperInvariant()))
                    .Select(p => Chiave(r.ProcedureId, p)));
            }
        }
        else
        {
            foreach (var s in new SidParser(avvisi).Parse(percorso, palette).Records)
            {
                if (!string.Equals(s.IcaoCode, icao, StringComparison.OrdinalIgnoreCase) || s.Name.Trim().Length == 0)
                {
                    continue;
                }

                delMotore.AddRange(Piste(s.Runway).Select(p => Chiave(s.Name, p)));
            }
        }

        // Multinsiemi: una SID ripetuta due volte nel file è due righe in vIPI, e dev'esserlo anche nel motore.
        var liberi = delMotore.ToList();
        var soloVipi = new List<string>();
        int concordi = 0;
        foreach (string chiave in diVipi)
        {
            if (liberi.Remove(chiave))
            {
                concordi++;
            }
            else
            {
                soloVipi.Add(chiave);
            }
        }

        return new Esito(concordi, Array.Empty<string>(), soloVipi, liberi, Array.Empty<string>());
    }

    /// <summary>
    /// La lettura di prova dei tag (lotto «Subito» slice 19, carta del lotto §5.3; «file per file» §M regola 10: «vIPI
    /// legge, non scrive»): su UN file.
    /// </summary>
    /// <param name="Etichettati">I record a cui il motore ha scritto la dichiarazione <c>//@"NOME" …</c>.</param>
    /// <param name="Oggetti">Gli oggetti che il lettore di vIPI legge dal file com'è oggi, senza tag.</param>
    /// <param name="Cambiati">Gli oggetti che vIPI legge diversi (o in più, o in meno) dal file coi tag: devono essere zero,
    /// perché per il lettore di oggi un tag è un commento.</param>
    /// <param name="ProcedureCoiLoroTag">Le SID e le STAR lette da vIPI che ritrovano per NOME i tag scritti dal motore,
    /// coi valori giusti: è la chiave con cui vIPI potrà riempire fix e salita iniziale.</param>
    /// <param name="ProcedureSenza">Quelle che non li ritrovano.</param>
    public sealed record LetturaDeiTag(int Etichettati, int Oggetti, IReadOnlyList<string> Cambiati,
                                       int ProcedureCoiLoroTag, IReadOnlyList<string> ProcedureSenza)
    {
        public bool Pulita => Cambiati.Count == 0 && ProcedureSenza.Count == 0;
    }

    /// <summary>La salita iniziale della prova: un valore con lo spazio, che nel tag va fra virgolette (§M regola 5).</summary>
    public const string SalitaDiProva = "COO APP";

    /// <summary>Le estensioni che il lettore di produzione di vIPI legge, e che la prova sa etichettare.</summary>
    public static IReadOnlyList<string> EstensioniLetteDaVipi { get; } = [".sid", ".str", ".fix", ".vor", ".ndb", ".mva", ".tfl", ".frq", ".ap", ".rw"];

    /// <summary>
    /// Etichetta ogni record del file come lo farebbe il Lab (la dichiarazione con le sue chiavi, il <c>//@source</c>
    /// del file, i <c>//@@</c> sui punti di SID e STAR) e lo passa al lettore di produzione di vIPI, com'è e coi tag.
    /// </summary>
    public static LetturaDeiTag DeiTag(string percorso)
    {
        string icao = Path.GetFileNameWithoutExtension(percorso).ToUpperInvariant();
        var nessunNome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nessunAlias = new Dictionary<string, string>();
        var avvisi = new Silenzio();
        var nota = new Dictionary<string, string> { ["note"] = "\"prova dei tag\"" };
        var delPunto = new Dictionary<string, string> { ["role"] = "IAF", ["alt"] = "+FL80", ["spd"] = "-210" };
        bool diRotta = percorso.Replace('\\', '/').Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase);
        bool fic = Path.GetFileName(percorso).EndsWith("fic.tfl", StringComparison.OrdinalIgnoreCase);

        return Path.GetExtension(percorso).ToLowerInvariant() switch
        {
            ".sid" => Prova(new SidParser(avvisi), new SidSaver(), Metadati.NomeSid, percorso, s => DiProcedura(s.Name), delPunto,
                t => AuroraSectorfileParser.ParseSids(icao, t, nessunNome, nessunAlias),
                (t, riletto) => Ritrova(AuroraSectorfileParser.ParseSids(icao, t, nessunNome, nessunAlias), Metadati.Leggi(riletto, Metadati.NomeSid).Record)),
            ".str" => Prova(new StrParser(avvisi), new StrSaver(), Metadati.NomeStr, percorso, s => DiProcedura(s.ProcedureId), delPunto,
                t => AuroraSectorfileParser.ParseStars(icao, t, nessunNome, nessunAlias),
                (t, riletto) => Ritrova(AuroraSectorfileParser.ParseStars(icao, t, nessunNome, nessunAlias), Metadati.Leggi(riletto, Metadati.NomeStr).Record)),
            ".fix" => Prova(new FixParser(avvisi), new FixSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseNavaids(new[] { (NavaidKind.Fix, (string?)t) }).Righe),
            ".vor" => Prova(new VorParser(avvisi), new VorSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseNavaids(new[] { (NavaidKind.Vor, (string?)t) }).Righe),
            ".ndb" => Prova(new NdbParser(avvisi), new NdbSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseNavaids(new[] { (NavaidKind.Ndb, (string?)t) }).Righe),
            ".mva" when diRotta => Prova(new MvaEnrouteParser(avvisi), new MvaSaver(enroute: true), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseMva(t)),
            ".mva" => Prova(new MvaAirportParser(avvisi), new MvaSaver(enroute: false), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseMva(t)),
            ".tfl" when fic => Prova(new FicParser(avvisi), new FicSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null, DeiSettori),
            ".tfl" => Prova(new TflParser(avvisi), new TflSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null, DeiSettori),
            ".frq" => Prova(new FrqParser(avvisi), new FrqSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseAtcPositions(t)),
            ".ap" => Prova(new ApParser(avvisi), new ApSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseAirports(t)),
            ".rw" => Prova(new RwParser(avvisi), new RwSaver(), r => Metadati.NomeDelRecord(r), percorso, _ => nota, null,
                t => AuroraSectorfileParser.ParseRunwayEnds(t)),
            var altra => throw new NotSupportedException($"Il lettore di vIPI non legge i file {altra}."),
        };

        // I due lettori dei .tfl di vIPI: le forme delle TWR e quelle dei settori (senza catalogo: i punti per nome
        // restano non risolti nello stesso modo prima e dopo).
        static object DeiSettori(string testo) => new object[]
        {
            AuroraSectorfileParser.ParseTowerShapes(testo).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => new { c.Key, Punti = c.Value.Select(p => new[] { p.Lat, p.Lon }) }),
            AuroraSectorfileParser.ParseSectorShapes(testo, NavaidCatalog.Empty),
        };
    }

    /// <summary>
    /// I tag di un <c>.sid</c> come li legge il motore: è la lettura che vIPI farà per riempire i suoi campi, con il
    /// nome della procedura per chiave.
    /// </summary>
    public static MetadatiDelFile<SidProcedure> TagDiUnSid(string percorso)
        => Metadati.Leggi(new SidParser(new Silenzio()).Parse(percorso, new ColorPalette()), Metadati.NomeSid);

    // Le chiavi di prova di una SID o di una STAR: il fix (dal nome, così ogni procedura ha il suo e uno scambio si
    // vede), la salita iniziale con lo spazio, la specifica.
    private static Dictionary<string, string> DiProcedura(string nome) => new()
    {
        ["fix"] = FixDiProva(nome),
        ["initialclimb"] = Metadati.ValoreDaScrivere(SalitaDiProva),
        ["nav"] = "RNAV1",
    };

    private static string FixDiProva(string nome)
        => new string(nome.Where(char.IsAsciiLetterOrDigit).Take(8).ToArray()).ToUpperInvariant() is { Length: > 0 } fix ? fix : "X";

    private static (int Con, List<string> Senza) Ritrova<T>(IReadOnlyList<SourceProcedure> diVipi, IReadOnlyList<MetadatiDelRecord<T>> tag)
    {
        var perNome = tag.GroupBy(m => m.Nome.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        int con = 0;
        var senza = new List<string>();
        foreach (var procedura in diVipi)
        {
            if (perNome.TryGetValue(procedura.Name.Trim(), out var suo)
                && Metadati.Testo(suo.Chiavi.GetValueOrDefault("fix") ?? "") == FixDiProva(procedura.Name)
                && Metadati.Testo(suo.Chiavi.GetValueOrDefault("initialclimb") ?? "") == SalitaDiProva)
            {
                con++;
            }
            else
            {
                senza.Add(Chiave(procedura.Name, procedura.Runway));
            }
        }

        return (con, senza);
    }

    private static LetturaDeiTag Prova<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, Func<T, string?> nomeDi, string percorso,
        Func<T, IReadOnlyDictionary<string, string>> chiaviDi, IReadOnlyDictionary<string, string>? delPunto,
        Func<string, object> diVipi, Func<string, ParseResult<T>, (int Con, List<string> Senza)>? ritrova = null)
        where T : class
    {
        var letto = lettore.Parse(percorso, new ColorPalette());
        var prima = Oggetti(diVipi(TestoDiProduzione(percorso)));
        if (letto.Records.Count == 0)
        {
            return new LetturaDeiTag(0, prima.Count, [], 0, []);
        }

        var etichettato = Metadati.ScriviSorgente(letto, nomeDi, "AIRAC2610");
        int etichettati = 0;
        foreach (var record in letto.Records)
        {
            try
            {
                etichettato = nomeDi(record) is null
                    ? Metadati.ScriviIlBlocco(etichettato, record, record, nomeDi, "PROVA", chiaviDi(record))
                    : Metadati.Scrivi(etichettato, record, nomeDi, chiaviDi(record));
                etichettati++;
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                // Un record che non si può dichiarare (il nome vuoto di `limf.sid:28`) resta senza tag: vIPI non lo legge nemmeno.
            }
        }

        if (delPunto is not null)
        {
            foreach (var record in letto.Records)
            {
                var righe = ((RecordChunk<T>)etichettato.Chunks.First(c => c is RecordChunk<T> r && ReferenceEquals(r.Record, record))).RawLines;
                for (int i = righe.Length - 1; i >= 1; i--)
                {
                    if (righe[i].Trim().Length > 0 && !righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal)
                        && Metadati.ChiaveDelPunto<T>(righe[i]) is { } punto && !punto.Contains('"', StringComparison.Ordinal))
                    {
                        etichettato = Metadati.ScriviIlPunto(etichettato, record, i, delPunto);
                    }
                }
            }
        }

        string temporaneo = Path.Combine(Path.GetTempPath(), "tag-per-vipi-" + Guid.NewGuid().ToString("N") + Path.GetExtension(percorso));
        try
        {
            new FileSaverOrchestrator().Save(etichettato, new HashSet<T>(), scrittore, temporaneo);
            string coiTag = TestoDiProduzione(temporaneo);
            var dopo = Oggetti(diVipi(coiTag));

            // Multinsiemi: ogni oggetto di prima deve esserci uguale dopo, e niente di più.
            var liberi = dopo.ToList();
            var cambiati = new List<string>();
            foreach (string oggetto in prima)
            {
                if (!liberi.Remove(oggetto))
                {
                    cambiati.Add("senza tag: " + Corto(oggetto));
                }
            }

            cambiati.AddRange(liberi.Select(o => "coi tag: " + Corto(o)));
            var (con, senza) = ritrova?.Invoke(coiTag, lettore.Parse(temporaneo, new ColorPalette())) ?? (0, []);
            return new LetturaDeiTag(etichettati, prima.Count, cambiati, con, senza);
        }
        finally
        {
            File.Delete(temporaneo);
        }
    }

    private static readonly JsonSerializerOptions ComeTesto = new()
    {
        IncludeFields = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    // Quel che vIPI ha letto, un testo per oggetto: una lista dà i suoi elementi, il resto un oggetto solo.
    private static List<string> Oggetti(object letto)
        => letto is IEnumerable elenco and not string
            ? [.. elenco.Cast<object>().Select(o => JsonSerializer.Serialize(o, ComeTesto))]
            : [JsonSerializer.Serialize(letto, ComeTesto)];

    private static string Corto(string oggetto) => oggetto.Length <= 160 ? oggetto : oggetto[..160] + "…";

    // Le piste del campo 2 come le divide vIPI: sui due punti, vuote tolte; nessuna = una voce senza pista.
    private static IEnumerable<string?> Piste(string campo)
    {
        var piste = campo.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return piste.Length == 0 ? new string?[] { null } : piste;
    }

    private static string Chiave(string nome, string? pista) => $"{nome.Trim().ToUpperInvariant()} pista {pista?.ToUpperInvariant() ?? "-"}";

    /// <summary>La forma pista di vIPI (<c>AuroraSectorfileParser.FormaPista</c>, privata): due cifre e il lato.</summary>
    private static readonly Regex FormaPista = new(@"^\d{2}[LRC]?$", RegexOptions.CultureInvariant);

    // Come arriva il file in produzione: HttpClient legge raw.githubusercontent come UTF-8.
    private static string TestoDiProduzione(string percorso) => File.ReadAllText(percorso, new System.Text.UTF8Encoding(false));

    private static string Scrivi(double lat, double lon) =>
        string.Create(CultureInfo.InvariantCulture, $"{lat:0.0000000};{lon:0.0000000}");

    /// <summary>La concordanza non guarda gli avvisi dei lettori: quelli li conta la misura delle righe opache.</summary>
    private sealed class Silenzio : IWarningCollector
    {
        public event EventHandler<LoadWarning>? WarningAdded;

        public int Count => 0;

        public void Add(LoadWarning warning) => WarningAdded?.Invoke(this, warning);

        public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
                        int? lineNumber = null, string? rawSnippet = null)
            => Add(new LoadWarning(severity, category, source, message, lineNumber, rawSnippet));

        public IReadOnlyList<LoadWarning> Snapshot() => Array.Empty<LoadWarning>();

        public void Clear()
        {
        }
    }
}
