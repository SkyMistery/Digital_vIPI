namespace Vipi.Infrastructure.Sectorfile;

/// <summary>Config del sectorfile Aurora su GitHub (sezione "Sectorfile"). Repo pubblico, raw, no auth.</summary>
public sealed class SectorfileOptions
{
    /// <summary>Base raw (deve terminare con "/"). Vuota = import SID disabilitato.</summary>
    public string RawBaseUrl { get; set; } = "";
    /// <summary>⚠️ <b>Ripiego.</b> I file di punti li elenca <c>ITALY.isc</c> (otto, non tre): questi tre
    /// percorsi si usano solo se l'indice non è raggiungibile o non cita nessun file di punti.
    /// Path del file fix relativo a <see cref="RawBaseUrl"/>.</summary>
    public string FixPath { get; set; } = "NAVAIDS/itfix.fix";
    /// <summary>Path del file VOR relativo a <see cref="RawBaseUrl"/>.</summary>
    public string VorPath { get; set; } = "NAVAIDS/itvor.vor";
    /// <summary>Path del file NDB relativo a <see cref="RawBaseUrl"/>. Assente = 404 = catalogo senza NDB.</summary>
    public string NdbPath { get; set; } = "NAVAIDS/itndb.ndb";
    /// <summary>
    /// URL <b>intero</b> del file maestro Aurora (<c>ITALY.isc</c>), da cui si ricava quali file di settore
    /// leggere. ⚠️ Non è un path relativo a <see cref="RawBaseUrl"/> perché sta un livello più su, e
    /// <c>..</c> su raw.githubusercontent non si risolve. Vuoto = niente shape di settore dal sectorfile.
    /// </summary>
    public string SectorIndexUrl { get; set; } =
        "https://raw.githubusercontent.com/ivao-italy/it-aurora-sector/master/SectorFiles/ITALY.isc";

    /// <summary>Path del file poligoni TWR (twrs.tfl) relativo a <see cref="RawBaseUrl"/>.</summary>
    public string TwrShapePath { get; set; } = "DYNAMIC_SEC/twrs.tfl";
    /// <summary>
    /// URL <b>intero</b> dell'elenco dei file di changelog della sorgente: da lì si legge il <b>ciclo AIRAC
    /// dichiarato</b>, che è il ciclo dal quale valgono le SID prelevate (carta 2026-09-02 §AW2). Il repo
    /// Aurora tiene un <c>CHANGELOG/&lt;ciclo&gt;.txt</c> per AIRAC, e il nome più alto è il ciclo pubblicato.
    /// <para>⚠️ Non è un path relativo a <see cref="RawBaseUrl"/> — è un altro host, <c>api.github.com</c> —
    /// per la stessa ragione di <see cref="SectorIndexUrl"/>. Vuoto = non si chiede, e il ciclo scende ai
    /// ripieghi: è una caduta dichiarata, non un guasto.</para>
    /// </summary>
    public string SidChangelogUrl { get; set; } =
        "https://api.github.com/repos/ivao-italy/it-aurora-sector/contents/SectorFiles/Include/IT/CHANGELOG";

    /// <summary>
    /// URL <b>intero</b> della API dei commit che dice <b>quando è cambiata l'ultima volta</b> la cartella dei
    /// file <c>.sid</c>. ⚠️ È il <b>ripiego</b> di <see cref="SidChangelogUrl"/>, e si chiede solo se il ciclo
    /// dichiarato non si è potuto leggere: dice quando i dati si sono mossi, non a quale ciclo appartengono.
    /// Vuoto = non si chiede.
    /// </summary>
    public string SidCommitsUrl { get; set; } =
        "https://api.github.com/repos/ivao-italy/it-aurora-sector/commits?path=SectorFiles/Include/IT&per_page=1";

    /// <summary>Ogni quante ore rilanciare l'import automatico.</summary>
    public int ImportHours { get; set; } = 24;
}
