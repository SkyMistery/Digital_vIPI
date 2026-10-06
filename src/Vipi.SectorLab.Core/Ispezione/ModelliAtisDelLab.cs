using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Un segnaposto che l'editor offre: di Aurora, o dichiarato da un <c>.fds</c> del sector.</summary>
/// <param name="Usato">Vero se il modello che si sta guardando lo usa già.</param>
public sealed record SegnapostoOfferto(string Nome, string Significato, bool Usato);

/// <summary>
/// Il modello che fa coppia con quello che si sta guardando (lotto «Subito» slice 18d, «file per file» W3): il D-ATIS
/// di un ATIS o l'ATIS di un D-ATIS, perché almeno una posizione di un <c>.frq</c> li usa insieme.
/// </summary>
/// <param name="Record">L'indice del suo modello nel file, o null se il file non ha modelli (<c>datis.datis</c>, vuoto).</param>
/// <param name="Posizioni">Quante righe dei <c>.frq</c> li usano insieme.</param>
/// <param name="SoloQui">I segnaposto che ha solo il modello che si sta guardando.</param>
/// <param name="SoloLa">I segnaposto che ha solo il compagno.</param>
public sealed record CompagnoDelModello(string File, int? Record, string Modello, int Posizioni,
                                        IReadOnlyList<string> SoloQui, IReadOnlyList<string> SoloLa)
{
    public bool Uguali => SoloQui.Count == 0 && SoloLa.Count == 0;
}

/// <summary>
/// I modelli ATIS e D-ATIS nel Lab (lotto «Subito» slice 18; «file per file» §22, W1 e W3): quali segnaposto offrire,
/// con quali valori riempire l'anteprima, e quale modello fa coppia con quale.
/// </summary>
public static class ModelliAtisDelLab
{
    /// <summary>Vero per un <c>.atis</c> o un <c>.datis</c>.</summary>
    public static bool EUnModello(string relativo)
        => relativo.EndsWith(".atis", StringComparison.OrdinalIgnoreCase) || EUnDatis(relativo);

    public static bool EUnDatis(string relativo) => relativo.EndsWith(".datis", StringComparison.OrdinalIgnoreCase);

    /// <summary>I campi in più dichiarati dai <c>.fds</c> della sessione, nell'ordine dei file.</summary>
    public static IReadOnlyList<(CampoDellAtis Campo, string File)> Campi(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        return [.. sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal)
            .Where(f => f is IFileConRecord)
            .SelectMany(f => ((IFileConRecord)f).RecordDelModello.OfType<CampoDellAtis>().Select(c => (c, f.Relativo)))];
    }

    /// <summary>
    /// I segnaposto da offrire per <paramref name="modello"/>: quelli che Aurora riempie da sé e quelli dei
    /// <c>.fds</c>, ognuno col suo significato e col segno «già usato».
    /// </summary>
    public static IReadOnlyList<SegnapostoOfferto> Segnaposto(SessioneAperta sessione, string modello)
    {
        var usati = ModelloAtis.Leggi(modello ?? "").Segnaposto.ToHashSet(StringComparer.Ordinal);
        var offerti = ModelloAtis.DiAurora.Select(s => new SegnapostoOfferto(s.Nome, s.Significato, usati.Contains(s.Nome))).ToList();
        foreach (var (campo, file) in Campi(sessione))
        {
            if (offerti.All(o => o.Nome != campo.Segnaposto))
                offerti.Add(new SegnapostoOfferto(campo.Segnaposto, $"il campo «{campo.Etichetta.Trim()}» di {Path.GetFileName(file)}", usati.Contains(campo.Segnaposto)));
        }

        return offerti;
    }

    /// <summary>
    /// I valori d'esempio con cui parte l'anteprima: lettera, ora, piste, livello, un METAR, il QFE. Le note
    /// (<c>REMARK</c>) e l'altitudine di transizione partono vuote, per vedere una parte facoltativa che sparisce.
    /// I campi dei <c>.fds</c> partono con «ILS» (l'unico del sector è il tipo di avvicinamento).
    /// </summary>
    public static Dictionary<string, string> Esempio(SessioneAperta sessione)
    {
        var valori = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STATION_NAME"] = "Roma Fiumicino",
            ["ATIS_LETTER"] = "A",
            ["ATIS_TIME"] = "1150",
            ["ARR"] = "16L",
            ["DEP"] = "25",
            ["DEP_FREQ"] = "130.900",
            ["TA"] = "",
            ["TL"] = "70",
            ["METAR"] = "LIRF 061150Z 24008KT 9999 FEW030 22/14 Q1018 NOSIG",
            ["REMARK"] = "",
            ["QFE"] = "1017",
            ["CPDLC"] = "LIRF",
        };
        foreach (var (campo, _) in Campi(sessione))
            valori.TryAdd(campo.Segnaposto, "ILS");
        return valori;
    }

    /// <summary>
    /// I compagni del modello di <paramref name="relativo"/>: per un <c>.atis</c> i D-ATIS che le posizioni dei
    /// <c>.frq</c> gli mettono accanto, per un <c>.datis</c> gli ATIS — dal più usato. Coi segnaposto che hanno solo
    /// l'uno o solo l'altro (tolti <c>STATION_NAME</c> e <c>CPDLC</c>, che è giusto siano diversi).
    /// </summary>
    public static IReadOnlyList<CompagnoDelModello> Compagni(SessioneAperta sessione, string relativo, string modello)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        bool datis = EUnDatis(relativo);
        string suoNome = Path.GetFileName(relativo);
        var quanti = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var posizione in sessione.File.Values.Where(f => f.Relativo.EndsWith(".frq", StringComparison.OrdinalIgnoreCase))
                     .OfType<IFileConRecord>().SelectMany(f => f.RecordDelModello.OfType<AtcPosition>()))
        {
            string? suo = Nome(datis ? posizione.DatisFile : posizione.AtisFile), altro = Nome(datis ? posizione.AtisFile : posizione.DatisFile);
            if (altro is not null && string.Equals(suo, suoNome, StringComparison.OrdinalIgnoreCase))
                quanti[altro] = quanti.GetValueOrDefault(altro) + 1;
        }

        var letto = ModelloAtis.Leggi(modello ?? "");
        var compagni = new List<CompagnoDelModello>();
        foreach (var (nome, posizioni) in quanti.OrderByDescending(q => q.Value).ThenBy(q => q.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (sessione.File.Values.FirstOrDefault(f => string.Equals(Path.GetFileName(f.Relativo), nome, StringComparison.OrdinalIgnoreCase)) is not { } file)
                continue;
            var record = (file as IFileConRecord)?.RecordDelModello;
            int indice = record?.ToList().FindIndex(r => r is AtisData) ?? -1;
            string testo = indice >= 0 ? ((AtisData)record![indice]).Template : "";
            var (soloQui, soloLa) = indice >= 0 ? ControlloDegliAtis.Differenze(letto, ModelloAtis.Leggi(testo)) : ([], []);
            compagni.Add(new CompagnoDelModello(file.Relativo, indice >= 0 ? indice : null, testo, posizioni, soloQui, soloLa));
        }

        return compagni;
    }

    // Il nome del file citato da un .frq, senza cartelle né la barra di troppo (`\liml.atis`).
    private static string? Nome(string? citato)
        => string.IsNullOrWhiteSpace(citato) ? null : Path.GetFileName(citato.Trim().Replace('\\', '/'));
}
