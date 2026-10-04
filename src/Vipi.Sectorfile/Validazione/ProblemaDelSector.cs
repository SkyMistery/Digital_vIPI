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

    /// <summary>
    /// Il confine dello scalo (<c>ad_boundary</c> in un <c>.geo</c>) senza la sua erba uguale in un <c>.pol</c>, o l'erba
    /// (<c>ad_boundary_Polygon</c>) senza il suo confine (lotto «Subito» slice 8d, H10): sono la stessa forma, e spostare
    /// il confine sposta l'erba. La calcola il Sector Lab, come <see cref="FormeDiverse"/>.
    /// </summary>
    ConfineSenzaErba,

    /// <summary>La stessa procedura due volte nello stesso file: scalo, piste, nome e tipo uguali (lotto «Subito» slice 9a, P3).</summary>
    ProceduraRipetuta,

    /// <summary>
    /// Il 6° campo di una SID non è il tipo (0 SID, 1 transizione): un campo manca prima (<c>VICTOR6A; ;0;VICTOR;</c>,
    /// slice 9a, P3) e Aurora legge la riga sbagliata.
    /// </summary>
    TipoFuoriPosto,

    /// <summary>Una procedura di un altro scalo nel file di uno scalo (<c>licz.str</c> con <c>LICC</c>, <c>limf.sid</c> con <c>LIMF18</c>: slice 9a, Q6, R-4).</summary>
    VoceDiUnAltroScalo,

    /// <summary>Una procedura su una pista che lo scalo non ha nei <c>.rw</c> (<c>lipi.str</c> <c>06:24</c> con 06L/06R: slice 9a, Q6, R-4).</summary>
    PistaInesistente,

    /// <summary>
    /// Una STAR che finisce in un punto dal quale nessun avvicinamento della sua pista passa, se la pista ne ha (lotto
    /// «Subito» slice 9e, Q2c: <see cref="LegamiDelleProcedure"/>).
    /// </summary>
    StarSenzaAvvicinamento,

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

    /// <summary>
    /// Un fix, VOR o NDB che cita un'attesa (6° campo; 8° di VOR e NDB) che non è fra quelle di <c>[HOLDENR]</c> del suo
    /// master: il tasto HOLD non la mostra (lotto «Subito» slice 10b, U1: <c>EKLAP</c> → <c>HLD-ELKAP</c>).
    /// </summary>
    AttesaNonDefinita,

    /// <summary>Un'attesa di <c>[HOLDENR]</c> che nessun fix, VOR o NDB del master cita: non si vede (slice 10b, U1).</summary>
    AttesaMaiCitata,

    /// <summary>
    /// Un'attesa lontana (0,1 NM o più) dal fix che la cita, o la cui info nomina un fix lontano dal suo punto
    /// (<c>HLD-EKLAP</c> con <c>ELKAP/090R-FL190</c>, a 143 NM: slice 10b, U1).
    /// </summary>
    AttesaFuoriPosto,

    /// <summary>
    /// Un master che carica fix, VOR o NDB che citano attese, ma non <c>[HOLDENR]</c>: nessuna si vede. Uno per master
    /// (committente, 29 settembre: sul fork i quattro master di FIR).
    /// </summary>
    AtteseNonCaricate,

    /// <summary>
    /// Lo stesso nome in due cataloghi (un VOR e un NDB) a 0,1 NM o più: un punto per nome non dice quale dei due
    /// (slice 10b, L3; committente, 29 settembre: 10 sul fork, <c>PIS</c> a 6,7 NM). Quale prende Aurora: prova di F4.
    /// </summary>
    NomeInPiuCataloghi,

    /// <summary>Un campo che il manuale vuole e la riga non ha: il tipo di un fix (9 in <c>VFR_NASCOSTI.fix</c>, slice 10b).</summary>
    CampoMancante,

    /// <summary>Un campo a valori fissi con un valore fuori elenco: il tipo <c>3:</c> di <c>APT.fix</c> (slice 10b).</summary>
    ValoreFuoriElenco,

    /// <summary>
    /// Nei trasferimenti di un <c>.frq</c> un include dopo un escluso: il manuale dice che «non funziona», Aurora non lo legge
    /// (lotto «Subito» slice 11a, M2; errore per il committente, 29 settembre: 102 sul fork). Col riordino proposto.
    /// </summary>
    IncludeDopoEscluso,

    /// <summary>Una posizione italiana nei trasferimenti che nessun <c>.frq</c> definisce (<c>LIMM_WN4_CTR</c>: slice 11a, M2).</summary>
    PosizioneNonDefinita,

    /// <summary>La stessa posizione due volte nello stesso <c>.frq</c> (<c>LIMF_WN0_APP</c> in <c>itfreq.frq</c>: slice 11a).</summary>
    PosizioneRipetuta,

    /// <summary>
    /// Una rotta di pista con decimali (<c>109.5</c>, <c>065.49</c>): Aurora la legge, ma rallenta (lotto «Subito» slice 11b,
    /// M4: 107 righe sul fork). Proposta al grado tondo; il Pannello la applica anche a tutto il file.
    /// </summary>
    RottaConDecimali,

    /// <summary>Il verso primario di una pista oltre il 18 (<c>LIMC;35R;17L</c>): il manuale lo vuole fra 01 e 18 (slice 11b, M4).</summary>
    PrimariaOltre18,

    /// <summary>
    /// La rotta scritta di una pista lontana più di 15° da quella delle soglie: soglie invertite o rotta sbagliata
    /// (<c>LIDW 15</c>, <c>LIKL 36</c>: slice 11b, M4).
    /// </summary>
    RottaDiversaDalleSoglie,

    /// <summary>Un gruppo CPDLC col nome in <c>[CPDLCNAMES]</c> e nessun messaggio (il 15, «TWR», sul fork: slice 11c, M5).</summary>
    GruppoSenzaMessaggi,

    /// <summary>Un messaggio CPDLC senza la risposta attesa (WU, AN, R, NE); non nel gruppo 20, il DCL (slice 11c, M5).</summary>
    MessaggioSenzaRisposta,

    /// <summary>Un messaggio CPDLC che dichiara un numero di valori (TotVal) diverso dai <c>[n]</c> del testo (2 sul fork, slice 11c).</summary>
    ValoriDelMessaggio,

    /// <summary>
    /// Una finestra PAR di un profilo senza didascalia, o con una pista che il <c>.rw</c> dello scalo non ha (<c>LIPI RWY06</c>:
    /// il <c>.rw</c> ha 06L e 06R; lotto «Subito» slice 11d, N3).
    /// </summary>
    ParSenzaPista,

    /// <summary>La radiale di un PAR a più di 0,2° dalla rotta della sua pista nel <c>.rw</c> (<c>LIBV</c>: 137 contro 138; slice 11d, N2).</summary>
    RadialeDelPar,

    /// <summary>L'elevazione di un PAR a più di 1 ft da quella della soglia nel <c>.rw</c> (slice 11d, N2).</summary>
    ElevazioneDelPar,

    /// <summary>Un profilo con più di 20 impostazioni fuori dai PAR: ognuna sovrascrive quella dell'utente (slice 11d, N3).</summary>
    ProfiloConMolteImpostazioni,

    /// <summary>
    /// Chiavi di una finestra finite nella sezione di un'altra (<c>INS4PAR_CAPTION</c> in <c>[INSET3]</c> di <c>LIBN.cpr</c>):
    /// un INI si legge per sezione e chiave, e lì Aurora non le legge (slice 11d, N3).
    /// </summary>
    ChiaveFuoriSezione,

    /// <summary>
    /// Uno stand o un'etichetta di taxiway con l'ICAO diverso da quello del suo file (<c>L3MC</c> in <c>limc.gts</c>,
    /// <c>LINB</c> in <c>libn.txi</c>): Aurora non lo mette a quello scalo. Entro 10 km dallo scalo del file è un refuso, e
    /// si propone l'ICAO del file; oltre (lo stand di <c>LIBP</c> in <c>libg.gts</c>) è nel file sbagliato (slice 12a, R3).
    /// </summary>
    ScaloDiversoDalFile,

    /// <summary>Uno stand o un'etichetta col suo ICAO ma a più di 10 km dal centro dello scalo (slice 12a, R3).</summary>
    LontanoDalloScalo,

    /// <summary>Lo stesso stand due volte nello stesso <c>.gts</c> (<c>lipk.gts</c> «404»: slice 12a, R3).</summary>
    StandRipetuto,

    /// <summary>
    /// Un'etichetta di taxiway a più di 100 m da ogni asse o bordo di taxiway del <c>.geo</c> del suo scalo (27 sul fork:
    /// slice 12a, R1).
    /// </summary>
    EtichettaLontanaDallaTaxiway,

    /// <summary>
    /// Il tipo di un segmento <c>.geo</c> vuoto (10 in <c>liap.geo</c>) o che non è un tipo dei <c>.geo</c>, né un nome
    /// di <c>colors.def</c>, né un colore; lo stesso per riempimento e bordo di un <c>.pol</c> (slice 12a, H2, I4).
    /// </summary>
    TipoSconosciuto,

    /// <summary>I riempimenti (<c>.pol</c>) di uno scalo che non ha il suo <c>.geo</c> coi bordi (slice 12a, I4).</summary>
    RiempimentoSenzaDisegno,

    /// <summary>
    /// Uno stand col tag <c>code</c> più grande di quello della taxiway che ha accanto (l'etichetta col codice più
    /// vicina, entro 300 m): l'aereo che ci sta non ci arriva (slice 12b, R6).
    /// </summary>
    StandPiuGrandeDellaTaxiway,

    /// <summary>
    /// Un riempimento di un <c>.pol</c> scritto dopo uno che gli sta sopra (<see cref="Shared.OrdineDeiRiempimenti"/>): in
    /// Aurora vince l'ultimo del file, e lo copre (slice 12d, I3; l'ordine è quello del fork, committente 4 ottobre 2026).
    /// </summary>
    OrdineDiDisegno,

    /// <summary>
    /// Un commento di marcature che nomina una pista che il <c>.rw</c> dello scalo non ha: rinumerata (<c>lirp.geo</c>
    /// 04/22, il <c>.rw</c> ha 03/21), o chiusa e tolta dal <c>.rw</c> (la 05/23 di LIBR: committente, 4 ottobre 2026).
    /// Slice 12d, O3.
    /// </summary>
    MarcaturaDiUnaPistaAssente,
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
            or Regola.CopieDiverse or Regola.FormeDiverse or Regola.ConfineSenzaErba or Regola.ProceduraRipetuta
            or Regola.VoceDiUnAltroScalo or Regola.PistaInesistente or Regola.StarSenzaAvvicinamento or Regola.CompostaNonAllineata or Regola.FormaQuasiChiusa
            or Regola.CommentoInCoda or Regola.FileCitatoAssente or Regola.FileInclusoDueVolte
            or Regola.FileNellaSezioneSbagliata or Regola.FileVuoto or Regola.CoordinataFuoriForma
            or Regola.NomeMancante or Regola.AttesaMaiCitata or Regola.AttesaFuoriPosto or Regola.AtteseNonCaricate
            or Regola.NomeInPiuCataloghi or Regola.CampoMancante or Regola.ValoreFuoriElenco
            or Regola.PosizioneNonDefinita or Regola.PosizioneRipetuta
            or Regola.RottaConDecimali or Regola.PrimariaOltre18 or Regola.RottaDiversaDalleSoglie
            or Regola.GruppoSenzaMessaggi or Regola.MessaggioSenzaRisposta or Regola.ValoriDelMessaggio
            or Regola.ParSenzaPista or Regola.RadialeDelPar or Regola.ElevazioneDelPar or Regola.ProfiloConMolteImpostazioni
            or Regola.ChiaveFuoriSezione
            or Regola.ScaloDiversoDalFile or Regola.LontanoDalloScalo or Regola.StandRipetuto
            or Regola.EtichettaLontanaDallaTaxiway or Regola.TipoSconosciuto or Regola.RiempimentoSenzaDisegno
            or Regola.StandPiuGrandeDellaTaxiway or Regola.OrdineDiDisegno or Regola.MarcaturaDiUnaPistaAssente => Validazione.Gravita.Avviso,
        _ => Validazione.Gravita.Errore,
    };
}
