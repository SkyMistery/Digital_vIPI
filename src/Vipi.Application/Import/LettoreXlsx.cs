using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Vipi.Application.Import;

/// <summary>Che cosa e' uscito da un <c>.xlsx</c>: la griglia, i fogli che c'erano, e il perche' se non e'
/// uscito niente.</summary>
/// <param name="Griglia">Le celle del foglio letto.</param>
/// <param name="Fogli">I nomi dei fogli, nell'ordine del file: servono a farne scegliere un altro.</param>
/// <param name="FoglioLetto">L'indice del foglio effettivamente letto.</param>
/// <param name="Guasto">Perche' non si e' letto niente; <c>null</c> se si e' letto.</param>
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
            var xml = Testo(archivio, fogli[scelto].Percorso);
            if (xml is null)
                return new EsitoXlsx(Griglia.Vuota, fogli.Select(f => f.Nome).ToList(), scelto,
                    "foglio non leggibile");

            var condivise = StringheCondivise(archivio);
            var righe = Celle(xml, condivise);
            return new EsitoXlsx(
                righe.Count == 0 ? Griglia.Vuota : new Griglia(righe, FormaGriglia.Xlsx),
                fogli.Select(f => f.Nome).ToList(), scelto);
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
        var libro = Testo(archivio, "xl/workbook.xml");
        var relazioni = Testo(archivio, "xl/_rels/workbook.xml.rels");
        if (libro is not null && relazioni is not null)
        {
            var mappa = XDocument.Parse(relazioni).Root?.Elements()
                .Where(e => e.Name.LocalName == "Relationship")
                .ToDictionary(
                    e => (string?)e.Attribute("Id") ?? "",
                    e => Normalizza((string?)e.Attribute("Target") ?? ""))
                ?? new Dictionary<string, string>();

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
        var xml = Testo(archivio, "xl/sharedStrings.xml");
        if (xml is null) return Array.Empty<string>();

        return XDocument.Parse(xml).Root?.Elements()
            .Where(e => e.Name.LocalName == "si")
            .Select(TestoDiSi)
            .ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    /// <summary>⚠️ Un <c>si</c> puo' essere spezzato in piu' <c>r</c> (pezzi con formattazione diversa): il
    /// testo e' la loro somma, e prendere il primo perderebbe meta' cella.</summary>
    private static string TestoDiSi(XElement si) =>
        string.Concat(si.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));

    private static IReadOnlyList<IReadOnlyList<string>> Celle(string xml, IReadOnlyList<string> condivise)
    {
        var righe = new List<IReadOnlyList<string>>();
        foreach (var r in XDocument.Parse(xml).Descendants().Where(e => e.Name.LocalName == "row"))
        {
            if (righe.Count >= MaxRighe) break;

            var celle = new List<string>();
            foreach (var c in r.Elements().Where(e => e.Name.LocalName == "c"))
            {
                var colonna = Colonna((string?)c.Attribute("r"));
                if (colonna >= 0)
                    while (celle.Count < colonna) celle.Add("");
                celle.Add(Valore(c, condivise));
            }
            if (celle.Any(x => x.Length > 0)) righe.Add(celle);
        }
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
        }
        return n - 1;
    }

    private static string Valore(XElement cella, IReadOnlyList<string> condivise)
    {
        var tipo = (string?)cella.Attribute("t") ?? "n";
        if (tipo == "inlineStr")
            return TestoTabellare.NormalizzaSegni(
                string.Concat(cella.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value)));

        var v = cella.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value;
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

    // ---- zip -----------------------------------------------------------------------------------------

    private static string? Testo(ZipArchive archivio, string percorso)
    {
        var voce = archivio.Entries.FirstOrDefault(
            e => e.FullName.Equals(percorso, StringComparison.OrdinalIgnoreCase));
        if (voce is null || voce.Length > MaxByteDecompresso) return null;

        using var flusso = voce.Open();
        using var limitato = new MemoryStream();
        var buffer = new byte[81920];
        int letti;
        while ((letti = flusso.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (limitato.Length + letti > MaxByteDecompresso) return null;
            limitato.Write(buffer, 0, letti);
        }

        limitato.Position = 0;
        using var lettore = new StreamReader(limitato, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return lettore.ReadToEnd();
    }
}
