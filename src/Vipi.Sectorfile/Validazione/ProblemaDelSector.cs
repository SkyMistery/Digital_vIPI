namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Una regola del validatore (carta F2 §3). Ognuna ha la sua gravità (<see cref="Regole.Gravita"/>): un errore è un
/// dato che Aurora legge male o non legge; un avviso si legge, ma è ambiguo, raro o fuori posto.
/// </summary>
public enum Regola
{
    /// <summary>Minuti o secondi ≥ 60, gradi oltre 90/180 (<c>itvor.vor:109</c> <c>N047.44.75.000</c>).</summary>
    CoordinataFuoriCampo,

    /// <summary>Un campo che comincia come una coordinata e non si legge (<c>E008-11.31.443</c>).</summary>
    CoordinataIllegibile,

    /// <summary>Lo SPAZIO al posto del <c>;</c> fra latitudine e longitudine (<c>N038.55.55.424 E016.36.08.523</c>, slice 6).</summary>
    SeparatoreSbagliato,

    /// <summary>Emisfero minuscolo (<c>itvor.vor:125</c> <c>n045.44.52.080</c>): il motore e vIPI lo leggono, la libreria A no.</summary>
    EmisferoMinuscolo,

    /// <summary>La frazione dei secondi non ha tre cifre (<c>N046.34.25.8735</c>): si legge, ma chi l'ha scritta intendeva millesimi?</summary>
    FrazioneAmbigua,

    /// <summary>Coppia decimale fuori dai <c>.txi</c> (legale, ma rara: <c>41.00850773;16.07432896;</c>).</summary>
    CoppiaDecimale,

    /// <summary>DMS e decimale nello stesso punto (vietato dalla specifica).</summary>
    DmsEDecimaleMescolati,

    /// <summary>Un campo obbligatorio vuoto (<c>itvor.vor:81</c> <c>GRO;;</c>).</summary>
    CampoVuoto,

    /// <summary>Una riga che il lettore non capisce, e nessuna regola più precisa dice perché.</summary>
    RigaIllegibile,

    /// <summary>Un punto per nome coi due campi diversi (<c>ALPHA SOUTH;ALPHA SUOTH</c>): Aurora prende la latitudine da uno e la longitudine dall'altro.</summary>
    DueNomiDiversi,

    /// <summary>Un poligono con meno di 3 vertici.</summary>
    PoligonoConPochiVertici,

    /// <summary>Un tag <c>//@</c> che non vale (slice 7): nome che non combacia, blocco spaiato, riga illeggibile.</summary>
    TagNonValido,

    /// <summary>Un tag <c>//@</c> che si legge ma è fuori catalogo o fuori posto.</summary>
    TagFuoriCatalogo,

    // Le regole dell'albero (slice 8b): vogliono tutti i file e gli .isc che li caricano.

    /// <summary>Un <c>F;</c> di un <c>.isc</c> che non porta a nessun file.</summary>
    FileCitatoAssente,

    /// <summary>Un file che nessun <c>.isc</c> carica: né <c>F;</c>, né per ICAO, né da un <c>.frq</c>.</summary>
    FileMaiCitato,

    /// <summary>Un punto per nome che non si trova nei cataloghi (fix, VOR, NDB, scali, VRP) di un <c>.isc</c> che carica il file.</summary>
    NomeNonRisolto,

    /// <summary>Lo stesso nome due volte nello stesso catalogo, a 0,1 NM o più: quale vale?</summary>
    NomeDuplicato,

    /// <summary>Lo stesso nome due volte nello stesso catalogo, a meno di 0,1 NM (la distanza è nel dettaglio).</summary>
    NomeRipetuto,
}

public enum Gravita
{
    Errore,
    Avviso,
}

/// <summary>Un problema del sector: la regola, il file (relativo alla cartella del sector), la riga (da 1) e il suo testo.</summary>
public sealed record ProblemaDelSector(Regola Regola, string File, int Riga, string Testo, string Dettaglio)
{
    public Gravita Gravita => Regole.Gravita(Regola);
}

public static class Regole
{
    public static Gravita Gravita(Regola regola) => regola switch
    {
        Regola.EmisferoMinuscolo or Regola.FrazioneAmbigua or Regola.CoppiaDecimale or Regola.DueNomiDiversi
            or Regola.TagFuoriCatalogo or Regola.FileMaiCitato or Regola.NomeRipetuto => Validazione.Gravita.Avviso,
        _ => Validazione.Gravita.Errore,
    };
}
