using System.Globalization;
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
