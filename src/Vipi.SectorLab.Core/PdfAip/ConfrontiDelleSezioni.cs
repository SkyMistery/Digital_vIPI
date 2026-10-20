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
/// <para>R7: <paramref name="Origine"/> è la riga di quel tipo, che i gesti (Porta, Sostituisci…) vogliono com'è;
/// <paramref name="DaGuardare"/> è vero anche per una riga uguale che ha un gesto da fare (l'uso di un fix, i tratti di una
/// rotta, un'area da dividere, una radioassistenza stampata diversa in ENR 4.1).</para>
/// </summary>
public sealed record RigaDelConfronto(EsitoDelConfronto Esito, string? Aip, string? Sector, string? File, int? Record, IReadOnlyList<string> Dettagli,
    object? Origine = null, bool DaGuardare = false);

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

    /// <summary>
    /// Gli strati del sector che tengono la sezione (i file di <see cref="Mappa.StratiDellaMappa.DiFile"/>): aprendo una
    /// sezione, sulla mappa del Confronto si accendono questi e basta (committente, 20 ottobre: la mappa piena non si legge).
    /// </summary>
    public static IReadOnlyList<string> StratiDelSector(SezioneDelConfronto sezione) => sezione switch
    {
        SezioneDelConfronto.Ctr or SezioneDelConfronto.Atz or SezioneDelConfronto.Procedure => ["procedure"],   // i MAPS e le procedure degli .str/.sid
        SezioneDelConfronto.AtzDelleTorri => ["settori"],                                                       // twrs.tfl
        SezioneDelConfronto.Rotte => ["aerovie"],
        SezioneDelConfronto.Attese => ["attese"],
        SezioneDelConfronto.Radioassistenze or SezioneDelConfronto.RadioassistenzeAd2 => ["radioassistenze"],
        SezioneDelConfronto.AreeEnr5 => ["aree"],
        SezioneDelConfronto.Piste => ["piste"],
        _ => ["punti"],                                                                                           // punti, scali (.ap), frequenze
    };

    /// <summary>I generi dello strato dell'AIP che tengono la sezione (quelli di <see cref="AipSullaMappa"/>).</summary>
    public static IReadOnlyList<string> GeneriDellAip(SezioneDelConfronto sezione) => sezione switch
    {
        SezioneDelConfronto.Ctr => ["CTR"],
        SezioneDelConfronto.Atz or SezioneDelConfronto.AtzDelleTorri => ["ATZ"],
        SezioneDelConfronto.Rotte => [AipSullaMappa.Rotte],
        SezioneDelConfronto.Attese => [AipSullaMappa.Attese],
        SezioneDelConfronto.Radioassistenze or SezioneDelConfronto.RadioassistenzeAd2 => [AipSullaMappa.Radioassistenze],
        SezioneDelConfronto.Punti => [AipSullaMappa.Punti],
        SezioneDelConfronto.AreeEnr5 => ["P", "R", "D", "TSA/TRA", "Militari"],
        SezioneDelConfronto.Procedure => [AipSullaMappa.Sid, AipSullaMappa.Star, AipSullaMappa.Transizioni, AipSullaMappa.SaliteIniziali],
        _ => [AipSullaMappa.Scali],                                                                               // scali, piste, frequenze
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

    /// <summary>Gli spazi (CTR, ATZ, ENR 5): la forma, a distanza, coi due scarti. Nelle aree di ENR 5 una voce del sector
    /// che tiene più aree dell'AIP è da guardare anche se uguale: si divide.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiForma> esiti, bool aree = false)
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
            return new RigaDelConfronto(e.Esito, e.Aip, e.Sector, e.File, e.Record, dettagli, e,
                e.Esito != EsitoDelConfronto.Uguale || (aree && e.Zone > 1 && e.File is not null));
        })];
    }

    /// <summary>Le procedure (F7): la SID o la STAR dell'AIP, i nomi con cui il sector la tiene.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiProcedura> esiti)
    {
        ArgumentNullException.ThrowIfNull(esiti);
        return [.. esiti.Select(e => new RigaDelConfronto(e.Esito,
            e.Esito == EsitoDelConfronto.SoloNelSector ? null : $"{e.Scalo} {e.Nome}",
            e.Nomi.Count > 0 ? string.Join(", ", e.Nomi) : null, e.File, e.Record, e.Dettagli, e, e.Esito != EsitoDelConfronto.Uguale))];
    }

    /// <summary>Rotte e attese (ENR 3). Una rotta uguale coi tratti da scrivere è da guardare.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiEnr3> esiti) =>
        Semplici(esiti, e => (e.Esito, e.Nome, e.File, e.Record, e.Dettagli, e.TrattiDaScrivere > 0));

    /// <summary>Radioassistenze e punti (ENR 4). Un punto con l'uso diverso è da guardare.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiEnr4> esiti) =>
        Semplici(esiti, e => (e.Esito, e.Nome, e.File, e.Record, e.Dettagli, e.UsoDiverso));

    /// <summary>Scali, piste, frequenze, radioassistenze di AD 2. Una diversa in ENR 4.1 è da guardare.</summary>
    public static IReadOnlyList<RigaDelConfronto> Da(IEnumerable<DifferenzaDiAd2> esiti) =>
        Semplici(esiti, e => (e.Esito, $"{e.Scalo} {e.Nome}", e.File, e.Record, e.Dettagli, e.DiversaInEnr41));

    // Le righe col solo nome: il nome è dell'AIP, e del sector se il sector ha la voce.
    private static IReadOnlyList<RigaDelConfronto> Semplici<T>(IEnumerable<T> esiti,
        Func<T, (EsitoDelConfronto Esito, string Nome, string? File, int? Record, IReadOnlyList<string> Dettagli, bool Anche)> parti)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(esiti);
        return [.. esiti.Select(e => (Origine: e, Parti: parti(e))).Select(x => new RigaDelConfronto(x.Parti.Esito,
            x.Parti.Esito == EsitoDelConfronto.SoloNelSector ? null : x.Parti.Nome,
            x.Parti.Esito == EsitoDelConfronto.MancaNelSector ? null : x.Parti.Nome,
            x.Parti.File, x.Parti.Record, x.Parti.Dettagli, x.Origine,
            x.Parti.Esito != EsitoDelConfronto.Uguale || x.Parti.Anche))];
    }

    /// <summary>Quante righe sono da guardare (non uguali, o uguali con un gesto da fare).</summary>
    public static int DaGuardare(IEnumerable<RigaDelConfronto> righe) => righe.Count(r => r.DaGuardare);

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
