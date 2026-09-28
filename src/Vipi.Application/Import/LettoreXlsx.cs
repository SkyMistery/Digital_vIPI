using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Vipi.Application.Import;

/// <summary>Che cosa e' uscito da un <c>.xlsx</c>: la griglia, i fogli che c'erano, e il perche' se non e'
/// uscito niente.</summary>
/// <param name="Griglia">Le celle del foglio letto.</param>
/// <param name="Fogli">I nomi dei fogli, nell'ordine del file: servono a farne scegliere un altro.</param>
/// <param name="FoglioLetto">L'indice del foglio effettivamente letto.</param>
/// <param name="Guasto">Perche' non si e' letto niente; <c>null</c> se si e' letto.</param>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record EsitoXlsx(
    Griglia Griglia, IReadOnlyList<string> Fogli, int FoglioLetto, string? Guasto = null);

/// <summary>
/// Legge un <c>.xlsx</c> <b>senza pacchetti</b>: e' uno zip con dentro dell'XML, e le due cose stanno gia'
/// nella libreria di base.
///
/// <para>
/// ⚠️ <b>Perche' non una libreria.</b> ClosedXML e OpenXml sanno fare cento cose di cui qui ne servono due —
/// leggere le celle di un foglio — e ognuna e' una dipendenza nuova da tenere allineata su <b>due</b> TFM,
/// con il <c>packages.lock.json</c> da rigenerare a ogni tocco. Centocinquanta righe che si leggono in
/// cinque minuti costano meno di un pacchetto che nessuno rilegge mai.
/// </para>
/// <para>
/// ⚠️ <b>Quel che NON fa, e va detto invece di scoprirlo.</b> Le date restano il numero seriale di Excel (il
/// formato sta altrove, in <c>styles.xml</c>, e nelle tabelle dei documenti le date non ci sono); le formule
/// danno il loro <b>ultimo risultato salvato</b>, che e' quel che si vedeva a schermo; le celle unite danno
/// il valore nella prima e il vuoto nelle altre, come il <c>colspan</c> dell'HTML.
/// </para>
/// <para>
/// ⚠️ I tetti sullo zip sono gli stessi del KMZ e per la stessa ragione: un file caricato da fuori non deve
/// poter decidere quanta memoria usare.
/// </para>
/// </summary>
public static class LettoreXlsx
{
    /// <summary>Il file caricato, compresso. Un foglio di tabelle di documento sta in pochissimo.</summary>
    public const int MaxByteFile = 8 * 1024 * 1024;

    /// <summary>Quanto si accetta di leggere di UNA voce, una volta aperto lo zip.</summary>
    public const int MaxByteDecompresso = 32 * 1024 * 1024;

    /// <summary>Quante voci puo' avere lo zip. Un xlsx normale ne ha una decina.</summary>
    public const int MaxVociZip = 400;

    /// <summary>Quante righe si leggono al massimo: oltre, non e' piu' una tabella di documento.</summary>
    public const int MaxRighe = 5000;

    /// <summary>L'ultima colonna di Excel, XFD. Oltre, il riferimento non viene da un foglio vero.</summary>
    public const int MaxColonne = 16384;

    /// <summary>
    /// 🔴 T-022 (revisione del 13 settembre 2026): quante celle in tutto, vuote comprese. Una cella in XFD riempie
    /// la riga di sedicimila posti: righe e colonne insieme vanno contate, non solo le righe.
    /// </summary>
    public const int MaxCelle = 200_000;

    /// <summary>
    /// Quante stringhe condivise al massimo. 🔴 U-045 (revisione totale 3): non avevano tetto, e un milione di
    /// <c>&lt;si&gt;</c> da pochi KB nello zip diventava un DOM da oltre 300 MB. Piu' stringhe distinte che celle non
    /// servono a nessun foglio leggibile.
    /// </summary>
    public const int MaxStringheCondivise = MaxCelle;

    /// <summary>Tetto dei file di struttura (cartella di lavoro e relazioni): pochi KB in un file vero. Si leggono
    /// ancora con <c>XDocument</c>, e senza un tetto loro erano 32 MB di DOM come il foglio.</summary>
    public const int MaxByteStruttura = 1024 * 1024;

    private static readonly EsitoXlsx Niente =
        new(Griglia.Vuota, Array.Empty<string>(), 0);

    /// <summary>
    /// Il foglio <paramref name="foglio"/> (indice 0) come griglia. Un file illeggibile non alza: torna con
    /// il <see cref="EsitoXlsx.Guasto"/> scritto, perche' chi ha appena caricato un file merita di sapere
    /// che cosa non andava, non una schermata d'errore.
    /// </summary>
    public static EsitoXlsx Leggi(Stream zip, int foglio = 0)
    {
        try
        {
            using var archivio = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            if (archivio.Entries.Count > MaxVociZip)
                return Niente with { Guasto = $"{archivio.Entries.Count} > {MaxVociZip} voci" };

            var fogli = Fogli(archivio);
            if (fogli.Count == 0) return Niente with { Guasto = "nessun foglio nel file" };

            var scelto = foglio >= 0 && foglio < fogli.Count ? foglio : 0;
            var nomi = fogli.Select(f => f.Nome).ToList();
            var voce = Voce(archivio, fogli[scelto].Percorso);
            if (voce is null) return new EsitoXlsx(Griglia.Vuota, nomi, scelto, "foglio non leggibile");

            // 🔴 U-045 (revisione totale 3): foglio e stringhe condivise si leggono in STREAMING, e i tetti scattano
            // durante la lettura. Un guasto qui lascia l'elenco dei fogli: chi ha scelto quello sbagliato ne sceglie
            // un altro, invece di ricaricare il file.
            try
            {
                var condivise = StringheCondivise(archivio);
                var righe = Celle(voce, condivise);
                return new EsitoXlsx(
                    righe.Count == 0 ? Griglia.Vuota : new Griglia(righe, FormaGriglia.Xlsx), nomi, scelto);
            }
            catch (InvalidDataException e) { return new EsitoXlsx(Griglia.Vuota, nomi, scelto, e.Message); }
            catch (XmlException e) { return new EsitoXlsx(Griglia.Vuota, nomi, scelto, e.Message); }
        }
        catch (InvalidDataException e) { return Niente with { Guasto = e.Message }; }
        catch (System.Xml.XmlException e) { return Niente with { Guasto = e.Message }; }
    }

    // ---- fogli ---------------------------------------------------------------------------------------

    private readonly record struct Foglio(string Nome, string Percorso);

    /// <summary>
    /// I fogli nell'ordine della cartella di lavoro, risolti attraverso le relazioni.
    /// <para>⚠️ L'ordine dei file <c>sheet1.xml, sheet2.xml…</c> <b>non</b> e' l'ordine delle schede: chi
    /// sposta una scheda in Excel non fa rinominare i file. Senza le relazioni, «il primo foglio» sarebbe
    /// il primo per nome di file, che a volte e' l'ultimo a schermo.</para>
    /// </summary>
    private static IReadOnlyList<Foglio> Fogli(ZipArchive archivio)
    {
        var libro = Testo(archivio, "xl/workbook.xml", MaxByteStruttura);
        var relazioni = Testo(archivio, "xl/_rels/workbook.xml.rels", MaxByteStruttura);
        if (libro is not null && relazioni is not null)
        {
            // T-022: un Id doppio faceva sollevare `ToDictionary` fuori dai catch. La prima relazione vince.
            var mappa = new Dictionary<string, string>();
            foreach (var e in XDocument.Parse(relazioni).Root?.Elements()
                         .Where(e => e.Name.LocalName == "Relationship") ?? Enumerable.Empty<XElement>())
                mappa.TryAdd((string?)e.Attribute("Id") ?? "", Normalizza((string?)e.Attribute("Target") ?? ""));

            var elenco = new List<Foglio>();
            foreach (var s in XDocument.Parse(libro).Descendants().Where(e => e.Name.LocalName == "sheet"))
            {
                var nome = (string?)s.Attributes().FirstOrDefault(a => a.Name.LocalName == "name") ?? "";
                var id = (string?)s.Attributes().FirstOrDefault(a => a.Name.LocalName == "id") ?? "";
                if (id.Length > 0 && mappa.TryGetValue(id, out var percorso) && Esiste(archivio, percorso))
                    elenco.Add(new Foglio(nome, percorso));
            }
            if (elenco.Count > 0) return elenco;
        }

        // Ripiego: i file dei fogli in ordine di nome. Un xlsx senza relazioni leggibili e' raro, ma
        // rifiutarlo del tutto sarebbe peggio che leggerlo con un ordine plausibile.
        return archivio.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                        && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(e => new Foglio(e.Name, e.FullName))
            .ToList();
    }

    private static string Normalizza(string target)
    {
        var t = target.Replace('\\', '/');
        if (t.StartsWith("/", StringComparison.Ordinal)) t = t.Substring(1);
        return t.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? t : "xl/" + t;
    }

    private static bool Esiste(ZipArchive archivio, string percorso) =>
        archivio.Entries.Any(e => e.FullName.Equals(percorso, StringComparison.OrdinalIgnoreCase));

    // ---- celle ---------------------------------------------------------------------------------------

    /// <summary>
    /// Le stringhe condivise. In un xlsx il testo delle celle non sta nelle celle: sta qui una volta sola, e
    /// la cella porta l'indice.
    /// </summary>
    private static IReadOnlyList<string> StringheCondivise(ZipArchive archivio)
    {
        var voce = Voce(archivio, "xl/sharedStrings.xml");
        if (voce is null) return Array.Empty<string>();

        var elenco = new List<string>();
        using var x = Lettore(voce);
        while (x.Read())
        {
            if (x.NodeType != XmlNodeType.Element || x.LocalName != "si") continue;
            if (elenco.Count >= MaxStringheCondivise)
                throw new InvalidDataException($"oltre {MaxStringheCondivise} stringhe condivise");
            elenco.Add(TestoDeiT(x));
        }
        return elenco;
    }

    /// <summary>
    /// Il testo di tutti i <c>&lt;t&gt;</c> dentro l'elemento su cui sta il lettore. ⚠️ Un <c>si</c> puo' essere
    /// spezzato in piu' <c>r</c> (pezzi con formattazione diversa): il testo e' la loro somma, che resta sulla sua chiusura.
    /// Gli spazi soli contano solo se dichiarati (<c>xml:space="preserve"</c>): come faceva <c>XDocument</c>.
    /// </summary>
    private static string TestoDeiT(XmlReader x)
    {
        if (x.IsEmptyElement) return "";
        var profondita = x.Depth;
        var sb = new StringBuilder();
        var dentroT = -1;
        while (x.Read())
        {
            if (x.NodeType == XmlNodeType.EndElement && x.Depth == profondita) break;
            if (x.NodeType == XmlNodeType.Element && x.LocalName == "t" && !x.IsEmptyElement) dentroT = x.Depth;
            else if (x.NodeType == XmlNodeType.EndElement && x.Depth == dentroT) dentroT = -1;
            else if (dentroT >= 0 && x.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                sb.Append(x.Value);
        }
        return sb.ToString();
    }

    private static IReadOnlyList<IReadOnlyList<string>> Celle(ZipArchiveEntry foglio, IReadOnlyList<string> condivise)
    {
        var righe = new List<IReadOnlyList<string>>();
        var totale = 0;
        List<string>? celle = null;

        void Chiudi()
        {
            if (celle is not null && celle.Any(v => v.Length > 0)) righe.Add(celle);
            celle = null;
        }

        using var x = Lettore(foglio);
        while (x.Read())
        {
            if (x.NodeType == XmlNodeType.EndElement && x.LocalName == "row") { Chiudi(); continue; }
            if (x.NodeType != XmlNodeType.Element) continue;

            if (x.LocalName == "row")
            {
                Chiudi();
                if (righe.Count >= MaxRighe) break;
                if (!x.IsEmptyElement) celle = new List<string>();
                continue;
            }
            if (x.LocalName != "c" || celle is null) continue;

            var riferimento = x.GetAttribute("r");
            var colonna = Colonna(riferimento);
            // 🔴 T-022: il riferimento decide quante celle vuote aggiungere, e viene dal file. Si controlla
            // PRIMA di allocare: una colonna oltre XFD, o troppe celle in tutto, e il file si rifiuta.
            if (colonna >= MaxColonne)
                throw new InvalidDataException($"colonna oltre XFD ({riferimento})");
            var dopo = totale + Math.Max(colonna, celle.Count) + 1 - celle.Count;
            if (dopo > MaxCelle)
                throw new InvalidDataException($"oltre {MaxCelle} celle");
            if (colonna >= 0)
                while (celle.Count < colonna) celle.Add("");
            celle.Add(Valore(x, condivise));
            totale = dopo;
        }
        Chiudi();
        return righe;
    }

    /// <summary>Da <c>BC12</c> a 54: l'indice della colonna, base 26 con le lettere. -1 se non c'e'.</summary>
    private static int Colonna(string? riferimento)
    {
        if (string.IsNullOrEmpty(riferimento)) return -1;
        var n = 0;
        foreach (var c in riferimento!)
        {
            if (c >= 'A' && c <= 'Z') n = n * 26 + (c - 'A' + 1);
            else if (c >= 'a' && c <= 'z') n = n * 26 + (c - 'a' + 1);
            else break;
            // Oltre XFD si smette di contare: con abbastanza lettere `n` traboccherebbe e tornerebbe negativo.
            if (n > MaxColonne) return MaxColonne;
        }
        return n - 1;
    }

    /// <summary>Il valore della cella su cui sta il lettore (che resta sulla sua chiusura).</summary>
    private static string Valore(XmlReader x, IReadOnlyList<string> condivise)
    {
        var tipo = x.GetAttribute("t") ?? "n";
        if (tipo == "inlineStr") return TestoTabellare.NormalizzaSegni(TestoDeiT(x));

        var v = ValoreDiV(x);
        if (v is null) return "";

        return tipo switch
        {
            "s" => int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var i)
                   && i >= 0 && i < condivise.Count
                ? TestoTabellare.NormalizzaSegni(condivise[i])
                : "",
            "b" => v == "1" ? "1" : "0",
            // ⚠️ Una cella d'errore (#N/D, #VALORE!) si legge VUOTA, non con il suo codice: importare
            // «#N/D» in un documento scriverebbe l'errore di Excel dentro una SOP.
            "e" => "",
            _ => TestoTabellare.NormalizzaSegni(v),
        };
    }

    /// <summary>Il testo del <c>&lt;v&gt;</c> figlio diretto della cella su cui sta il lettore; null se non c'e'.</summary>
    private static string? ValoreDiV(XmlReader x)
    {
        if (x.IsEmptyElement) return null;
        var profondita = x.Depth;
        StringBuilder? v = null;
        var dentroV = false;
        while (x.Read())
        {
            if (x.NodeType == XmlNodeType.EndElement && x.Depth == profondita) break;
            if (x.NodeType == XmlNodeType.Element && x.Depth == profondita + 1 && x.LocalName == "v")
            {
                v ??= new StringBuilder();
                dentroV = !x.IsEmptyElement;
            }
            else if (x.NodeType == XmlNodeType.EndElement && x.Depth == profondita + 1) dentroV = false;
            else if (dentroV && x.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                v!.Append(x.Value);
        }
        return v?.ToString();
    }

    // ---- zip -----------------------------------------------------------------------------------------

    private static ZipArchiveEntry? Voce(ZipArchive archivio, string percorso)
    {
        var voce = archivio.Entries.FirstOrDefault(
            e => e.FullName.Equals(percorso, StringComparison.OrdinalIgnoreCase));
        return voce is null || voce.Length > MaxByteDecompresso ? null : voce;
    }

    /// <summary>
    /// Un lettore XML in streaming sulla voce dello zip, con il tetto sui byte decompressi applicato MENTRE si legge
    /// (la lunghezza dichiarata nello zip la scrive chi fa il file). Niente DTD, niente risorse esterne.
    /// </summary>
    private static XmlReader Lettore(ZipArchiveEntry voce) =>
        XmlReader.Create(new FlussoLimitato(voce.Open(), MaxByteDecompresso), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = true,
        });

    /// <summary>Un flusso che smette di dare byte oltre un tetto: oltre, <see cref="InvalidDataException"/>.</summary>
    private sealed class FlussoLimitato(Stream interno, long tetto) : Stream
    {
        private long _letti;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = interno.Read(buffer, offset, count);
            _letti += n;
            if (_letti > tetto) throw new InvalidDataException($"voce oltre {tetto / (1024 * 1024)} MB decompressi");
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _letti; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) interno.Dispose();
            base.Dispose(disposing);
        }
    }

    private static string? Testo(ZipArchive archivio, string percorso, int tetto)
    {
        var voce = archivio.Entries.FirstOrDefault(
            e => e.FullName.Equals(percorso, StringComparison.OrdinalIgnoreCase));
        if (voce is null || voce.Length > tetto) return null;

        using var flusso = voce.Open();
        using var limitato = new MemoryStream();
        var buffer = new byte[81920];
        int letti;
        while ((letti = flusso.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (limitato.Length + letti > tetto) return null;
            limitato.Write(buffer, 0, letti);
        }

        limitato.Position = 0;
        using var lettore = new StreamReader(limitato, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return lettore.ReadToEnd();
    }
}
