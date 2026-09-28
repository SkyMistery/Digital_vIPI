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

    /// <summary>
    /// Un <c>F;</c> di un <c>.isc</c> col percorso sbagliato. Avviso dal lotto «Subito» slice 2b (carta «file per file»
    /// §C, M6): Aurora trova il file anche per nome (<c>DYNAMIC_SEC\GCI.tfl</c> sta in <c>OTHER\</c>), e il dettaglio
    /// dice dove; se il file non c'è da nessuna parte, lo dice.
    /// </summary>
    FileCitatoAssente,

    /// <summary>
    /// Un file che nessun <c>.isc</c> carica: né <c>F;</c>, né per ICAO, né da un <c>.frq</c>. Se le sue coordinate stanno
    /// in un file caricato, il dettaglio dice «è una copia di…» (V2).
    /// </summary>
    FileMaiCitato,

    /// <summary>Un punto per nome che non si trova nei cataloghi (fix, VOR, NDB, scali, VRP) di un <c>.isc</c> che carica il file.</summary>
    NomeNonRisolto,

    /// <summary>Lo stesso nome due volte nello stesso catalogo, a 0,1 NM o più: quale vale?</summary>
    NomeDuplicato,

    /// <summary>Lo stesso nome due volte nello stesso catalogo, a meno di 0,1 NM (la distanza è nel dettaglio).</summary>
    NomeRipetuto,

    /// <summary>
    /// Una copia gemella diversa dalle altre (carta F3-bis §2.1): lo stesso scalo, pista o posizione con un altro valore
    /// nel file nazionale e in quello della FIR (<c>LIBA</c> a 182 ft in <c>itap.ap</c>, a 185 in <c>libb.ap</c>).
    /// </summary>
    CopieDiverse,

    /// <summary>
    /// Una famiglia di forme dichiarata (<c>form=NOME</c>, lotto «Subito» slice 8b, D5) con una copia di forma diversa
    /// dalle altre: il settore dinamico e il suo confine non disegnano più la stessa cosa. La calcola il Sector Lab, che ha
    /// le forme coi nomi risolti; il validatore dell'albero non la dà.
    /// </summary>
    FormeDiverse,

    /// <summary>Una mappa composta elenca una procedura che nel suo <c>.str</c> non c'è (F3-bis §2.2), o un elenco che non si legge.</summary>
    CompostaConProceduraAssente,

    /// <summary>
    /// Una mappa composta diversa da come la rigenererebbe il Lab: qualcuno l'ha cambiata a mano, o una procedura è
    /// cambiata fuori dal Lab (F3-bis §2.2).
    /// </summary>
    CompostaNonAllineata,

    /// <summary>
    /// Una forma (settore, zona di uno .str, MVA, poligono) il cui ultimo punto QUASI ripete il primo: diverso per una
    /// sola cifra (<c>N041.52.31</c> contro <c>N040.52.31</c>, il refuso del committente del 24 settembre in
    /// <c>lirn.str</c> «LIRN ATZ»). Aperta o chiusa per sbaglio: Aurora la disegna com'è.
    /// </summary>
    FormaQuasiChiusa,

    /// <summary>
    /// Commenti in coda a righe di dati (<c>T;BREAK;RIVAM;RIVAM; //discontinuity</c>, lotto «Subito» slice 2): uno per
    /// file, col numero e le righe. Aurora li legge, ma una riga così le costa circa il doppio del tempo.
    /// </summary>
    CommentoInCoda,

    /// <summary>Lo stesso file citato due volte da un <c>.isc</c> (<c>lirrctr.tfl</c> in <c>ITALY.isc</c>, D7): Aurora lo carica due volte.</summary>
    FileInclusoDueVolte,

    /// <summary>
    /// Un file sotto una sezione dell'<c>.isc</c> che non ha la sua forma (F6): i punti VFR di <c>ENRVFI</c> sotto
    /// <c>[VFRENR]</c>, che vuole le rotte.
    /// </summary>
    FileNellaSezioneSbagliata,

    /// <summary>Un file senza una riga di dati (<c>ACC\test.artcc</c>, incluso da <c>ITALY.isc</c>: A9).</summary>
    FileVuoto,

    /// <summary>
    /// Una coordinata che si legge col suo valore ma non ha la forma giusta (lotto «Subito» slice 2c): gradi senza lo
    /// zero davanti (<c>E12.30.18.200</c>), un compatto con una cifra in meno (<c>E103441000</c>). Con la proposta.
    /// </summary>
    CoordinataFuoriForma,

    /// <summary>
    /// Un compatto che il motore e vIPI leggono in un altro punto, perché lo leggono da destra (lotto «Subito» slice 2c):
    /// con una cifra in più (<c>E01221856000</c> in <c>lirf.vfi</c> e <c>VFR_NASCOSTI.fix</c> = E122.18.56, in Asia; quale
    /// cifra è di troppo non si sa, nessuna proposta), o con una in meno in fondo (<c>N041131620</c> in <c>MIL.fix</c> =
    /// N004.11.31, in Africa; proposta N0411316200).
    /// </summary>
    CoordinataLettaAltrove,

    /// <summary>
    /// Gruppi di un <c>.geo</c> o di un <c>.pol</c> col nome che mette Google Earth, «Percorso senza titolo» o «Poligono
    /// senza titolo» (lotto «Subito» slice 6, «file per file» H3: 564 nei <c>.geo</c> del fork): il nome del gruppo è il
    /// commento sopra, e questo non dice niente. Uno per file, col numero e le righe.
    /// </summary>
    NomeMancante,
}

public enum Gravita
{
    Errore,
    Avviso,
}

/// <summary>
/// Un problema del sector: la regola, il file (relativo alla cartella del sector), la riga (da 1) e il suo testo. La
/// <paramref name="Proposta"/> è la riga corretta, quando il validatore la sa (lotto «Subito» slice 2c: le coordinate
/// scritte male); il Lab la scrive solo se l'AOD la sceglie.
/// </summary>
public sealed record ProblemaDelSector(Regola Regola, string File, int Riga, string Testo, string Dettaglio, string? Proposta = null)
{
    public Gravita Gravita => Regole.Gravita(Regola);
}

public static class Regole
{
    public static Gravita Gravita(Regola regola) => regola switch
    {
        Regola.EmisferoMinuscolo or Regola.FrazioneAmbigua or Regola.CoppiaDecimale or Regola.DueNomiDiversi
            or Regola.TagFuoriCatalogo or Regola.FileMaiCitato or Regola.NomeRipetuto
            or Regola.CopieDiverse or Regola.FormeDiverse or Regola.CompostaNonAllineata or Regola.FormaQuasiChiusa
            or Regola.CommentoInCoda or Regola.FileCitatoAssente or Regola.FileInclusoDueVolte
            or Regola.FileNellaSezioneSbagliata or Regola.FileVuoto or Regola.CoordinataFuoriForma
            or Regola.NomeMancante => Validazione.Gravita.Avviso,
        _ => Validazione.Gravita.Errore,
    };
}
