using System.Globalization;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le sezioni dello spazio Confronti (riordino R6, D17): una per confronto che il Lab sa fare fra l'AIP espanso e il
/// sector, nell'ordine dell'AIP.
/// </summary>
public enum SezioneDelConfronto
{
    Ctr,
    Atz,
    AtzDelleTorri,
    Rotte,
    Attese,
    Radioassistenze,
    Punti,
    AreeEnr5,
    Scali,
    Piste,
    Frequenze,
    RadioassistenzeAd2,
    Procedure,
}

/// <summary>
/// Una riga del Confronto, uguale per tutte le sezioni (R6): l'esito, la voce dell'AIP, quella del sector (e dove sta,
/// per aprirla), e le righe che dicono la differenza. Viene dalle righe dei confronti di F6 e F7, ognuna col suo tipo.
/// </summary>
public sealed record RigaDelConfronto(EsitoDelConfronto Esito, string? Aip, string? Sector, string? File, int? Record, IReadOnlyList<string> Dettagli);

/// <summary>Le sezioni del Confronto: nomi, parti dell'AIP, e le righe dei confronti di oggi portate a una forma sola.</summary>
public static class ConfrontiDelleSezioni
{
    /// <summary>Il nome della sezione come lo legge un AOD.</summary>
    public static string Nome(SezioneDelConfronto sezione) => sezione switch
    {
        SezioneDelConfronto.Ctr => "CTR",
        SezioneDelConfronto.Atz => "ATZ",
        SezioneDelConfronto.AtzDelleTorri => "ATZ nei settori delle torri",
        SezioneDelConfronto.Rotte => "Rotte ATS",
        SezioneDelConfronto.Attese => "Attese",
        SezioneDelConfronto.Radioassistenze => "Radioassistenze",
        SezioneDelConfronto.Punti => "Punti significativi",
        SezioneDelConfronto.AreeEnr5 => "Aree P, R, D, TSA/TRA e militari",
        SezioneDelConfronto.Scali => "Scali",
        SezioneDelConfronto.Piste => "Piste",
        SezioneDelConfronto.Frequenze => "Frequenze",
        SezioneDelConfronto.RadioassistenzeAd2 => "Radioassistenze degli scali",
        SezioneDelConfronto.Procedure => "SID e STAR",
        _ => sezione.ToString(),
    };

    /// <summary>La parte dell'AIP: le sezioni si raggruppano come nell'Indice.</summary>
    public static string Parte(SezioneDelConfronto sezione) => sezione switch
    {
        SezioneDelConfronto.Ctr or SezioneDelConfronto.Atz or SezioneDelConfronto.AtzDelleTorri => "ENR 2",
        SezioneDelConfronto.Rotte or SezioneDelConfronto.Attese => "ENR 3",
        SezioneDelConfronto.Radioassistenze or SezioneDelConfronto.Punti => "ENR 4",
        SezioneDelConfronto.AreeEnr5 => "ENR 5",
        _ => "AD 2",
    };

    /// <summary>Quante righe per esito.</summary>
    public static IReadOnlyDictionary<EsitoDelConfronto, int> Conteggi(IEnumerable<RigaDelConfronto> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var conteggi = Enum.GetValues<EsitoDelConfronto>().ToDictionary(e => e, _ => 0);
        foreach (var riga in righe)
            conteggi[riga.Esito]++;
        return conteggi;
    }

    /// <summary>Gli spazi (CTR, ATZ, ENR 5): la forma, a distanza, coi due scarti.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiForma> esiti)
    {
        ArgumentNullException.ThrowIfNull(esiti);
        return [.. esiti.Select(e =>
        {
            var dettagli = new List<string>();
            if (e.Zone > 1)
                dettagli.Add($"{e.Zone} zone nell'AIP");
            if (e.ScartoAip is { } aip)
                dettagli.Add($"l'AIP si stacca dal sector fino a {Nm(aip)} NM");
            if (e.ScartoSector is { } sector)
                dettagli.Add($"il sector si stacca dall'AIP fino a {Nm(sector)} NM");
            if (e.SenzaTracciato)
                dettagli.Add("un pezzo lungo un confine o una costa che l'AIP nomina senza forma: non si giudica");
            return new RigaDelConfronto(e.Esito, e.Aip, e.Sector, e.File, e.Record, dettagli);
        })];
    }

    /// <summary>Le procedure (F7): la SID o la STAR dell'AIP, i nomi con cui il sector la tiene.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiProcedura> esiti)
    {
        ArgumentNullException.ThrowIfNull(esiti);
        return [.. esiti.Select(e => new RigaDelConfronto(e.Esito,
            e.Esito == EsitoDelConfronto.SoloNelSector ? null : $"{e.Scalo} {e.Nome}",
            e.Nomi.Count > 0 ? string.Join(", ", e.Nomi) : null, e.File, e.Record, e.Dettagli))];
    }

    /// <summary>Rotte e attese (ENR 3).</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiEnr3> esiti) =>
        Semplici(esiti, e => (e.Esito, e.Nome, e.File, e.Record, e.Dettagli));

    /// <summary>Radioassistenze e punti (ENR 4).</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiEnr4> esiti) =>
        Semplici(esiti, e => (e.Esito, e.Nome, e.File, e.Record, e.Dettagli));

    /// <summary>Scali, piste, frequenze, radioassistenze di AD 2.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiAd2> esiti) =>
        Semplici(esiti, e => (e.Esito, $"{e.Scalo} {e.Nome}", e.File, e.Record, e.Dettagli));

    // Le righe col solo nome: il nome è dell'AIP, e del sector se il sector ha la voce.
    private static IReadOnlyList<RigaDelConfronto> Semplici<T>(IEnumerable<T> esiti,
        Func<T, (EsitoDelConfronto Esito, string Nome, string? File, int? Record, IReadOnlyList<string> Dettagli)> parti)
    {
        ArgumentNullException.ThrowIfNull(esiti);
        return [.. esiti.Select(parti).Select(p => new RigaDelConfronto(p.Esito,
            p.Esito == EsitoDelConfronto.SoloNelSector ? null : p.Nome,
            p.Esito == EsitoDelConfronto.MancaNelSector ? null : p.Nome,
            p.File, p.Record, p.Dettagli))];
    }

    private static string Nm(double valore) => valore.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));

    /// <summary>
    /// La voce dello strato dell'AIP di una riga, per il nome (🟡 il nome è quello del confronto, non sempre il titolo della
    /// voce): prima uguale, poi una voce che comincia col nome e uno spazio («OST» → «OST VOR/DME», «Bologna CTR» → «Bologna
    /// CTR Zona '1'»). Null se non c'è.
    /// </summary>
    public static int? VoceDelNome(IReadOnlyList<VoceDellAip> voci, string? nome)
    {
        ArgumentNullException.ThrowIfNull(voci);
        if (string.IsNullOrWhiteSpace(nome))
            return null;
        for (int i = 0; i < voci.Count; i++)
            if (string.Equals(voci[i].Titolo, nome, StringComparison.OrdinalIgnoreCase))
                return i;
        for (int i = 0; i < voci.Count; i++)
            if (voci[i].Titolo.StartsWith(nome + " ", StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }
}
