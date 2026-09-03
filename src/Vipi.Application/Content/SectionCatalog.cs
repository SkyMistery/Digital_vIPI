namespace Vipi.Application.Content;

/// <summary>
/// Catalogo UNIFICATO delle sezioni documentali (doc refactor 08a). Fonte unica per: la natura di ogni sezione
/// (<see cref="KindOf"/>), la membership per profilo (<see cref="For"/>), chi ne rende il corpo
/// (<see cref="IsHostRendered"/>, doc 13 §3a) e quali sono obbligatorie (<see cref="IsFixed"/>). Sostituisce i tre
/// registry per-tipo e l'enum <c>BlockSection</c>. Dalla carta 2026-08-26 partecipano <b>tutte e quattro</b> le
/// famiglie: l'aeroporto era l'ultima fuori, con un documento cotto a ogni rebuild e sezioni riconosciute per titolo.
/// <para>
/// Non c'è più una <c>Reconcile</c> d'ordine: dal doc 11 §3b «si itera la lista di sezioni del documento», non un
/// elenco di chiavi riconciliato a view-time. Il metodo era rimasto senza chiamanti, con il commento che lo
/// annunciava ancora come una delle responsabilità della fonte unica.
/// </para>
/// </summary>
public static class SectionCatalog
{
    // Natura di ogni sezione fissa — fonte unica: "aor" è Derived ovunque, ecc.
    private static readonly IReadOnlyDictionary<string, SectionKind> KindByKey =
        new Dictionary<string, SectionKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["aor"] = SectionKind.Derived,
            ["frequencies"] = SectionKind.Derived,
            ["coordination"] = SectionKind.Derived,
            ["sids"] = SectionKind.Derived,   // aeroporto (doc 10 §3e): SID derivata a view-time, non più cotta
            // Aeroporto (carta 2026-08-26): il contenuto sta nelle tabelle del profilo e si deriva a view-time,
            // esattamente come «aor»/«frequencies» sull'APP. Prima erano tabelle Markdown cotte nei blocchi.
            ["weather"] = SectionKind.Derived,        // METAR/TAF live dal NOAA
            ["runwayrules"] = SectionKind.Derived,    // regole di scelta pista (vento/superficie)
            ["transition"] = SectionKind.Derived,     // TA + tabella dei livelli di transizione per fascia QNH
            ["runways"] = SectionKind.Derived,        // piste dell'anagrafica IVAO + arricchimenti editoriali
            // ⚠️ IL TITOLO E' «MRVA», e resta uguale in tutte e due le lingue: e' la sigla con cui la si
            // chiama in frequenza e sulle carte, e come «SID» o «AOR» non si traduce (decisione del
            // committente, docs/design/regole-lingua.md). Prima diceva «Minime di vettoramento», che in
            // inglese il motore rendeva «Minimum vectoring» — giusto a meta', e comunque non la sigla.
            // «minima» è tornata Derived: le MRVA si prendono dal sectorfile come CARTA (non come tabella), una
            // per file .mva, e la pagina la disegna. La decisione del 2026-08-09 che le dichiarava non importabili
            // riguardava la tabella area→quota, che il formato davvero non permette di ricostruire; il disegno sì,
            // ed è quello che il controllore vede in Aurora. Vedi lavori-aperti §E2.
            ["minima"] = SectionKind.Derived,
            ["purpose"] = SectionKind.Editorial,   // vLOA: scopo dell'accordo, prosa (doc 13 §3c)
            ["separations"] = SectionKind.Editorial,
            ["configurations"] = SectionKind.Editorial,
            ["vfr"] = SectionKind.Editorial,
            ["regulated"] = SectionKind.Editorial,
            ["operationaltechnique"] = SectionKind.Editorial,
            // «Validità e revisione» deriva il suo timbro dalla RELEASE che si sta mostrando — ciclo, data e chi
            // ha premuto Pubblica — e sotto tiene il testo scritto a mano. Derivata, quindi, ma sempre live.
            ["validity"] = SectionKind.Derived,

            // --- vSOP militari (carta 2026-08-27): tutte EDITORIALI tranne quelle riusate sopra. ---
            // ⚠️ Si parte con tutto editoriale, e non e' pigrizia: la tabella ATC/CRC di un SOP elenca
            // anche l'APP di UN ALTRO campo e i CRC/AEW, che nel catalogo settori non esistono. Derivare
            // paga dove la sorgente ha davvero il dato; qui la sorgente e' un PDF, e il confine di
            // un'estrazione si misura prima di tagliare.
            ["generaldata"] = SectionKind.Editorial,
            // ⚠️ DERIVATA dal 29 agosto 2026 (carta §12): il corpo non è più prosa libera ma una TABELLA le
            // cui righe stanno nell'anagrafica di divisione — la stessa radioassistenza esce uguale nel SOP di
            // Amendola e in quello di Gioia. Il documento porta quali righe cita e in che ordine; i valori li
            // porta l'anagrafica, e la release li CONGELA come le altre derivate: senza, una frequenza
            // corretta oggi cambierebbe da sola un documento pubblicato al ciclo scorso.
            ["navaids"] = SectionKind.Derived,
            // Derivata come «navaids», e per la stessa ragione: gli scali e le loro radioassistenze si
            // risolvono sui cataloghi, e la release deve fotografarli.
            ["diversion"] = SectionKind.Derived,
            ["callsigns"] = SectionKind.Editorial,
            ["groundprocedures"] = SectionKind.Editorial,
            ["parkings"] = SectionKind.Editorial,
            ["enginestart"] = SectionKind.Editorial,
            ["taxiing"] = SectionKind.Editorial,
            ["arming"] = SectionKind.Editorial,
            ["flightprocedures"] = SectionKind.Editorial,
            ["takeoff"] = SectionKind.Editorial,
            ["sfo"] = SectionKind.Editorial,
            ["commfail"] = SectionKind.Editorial,
            ["gca"] = SectionKind.Editorial,
            ["vfrjet"] = SectionKind.Editorial,
            ["ifrsignificant"] = SectionKind.Editorial,
            ["gat"] = SectionKind.Editorial,
            ["qra"] = SectionKind.Editorial,
            ["lowlevel"] = SectionKind.Editorial,
        };

    /// <summary>Natura della sezione con questa chiave (Editorial se sconosciuta = custom).</summary>
    public static SectionKind KindOf(string key) => KindByKey.TryGetValue(key, out var k) ? k : SectionKind.Editorial;

    // Sezioni che nascono COLLASSATE (doc 11 §3i): quelle il cui contenuto è voluminoso per natura — «Aree
    // regolamentate» su una ACC sono decine di aree (105 su LIRR), e aperta la sezione occupa il documento
    // da sola. Vale OVUNQUE: viewer ed editor, tutte e tre le famiglie.
    // ⚠️ Qui c'era scritto «ognuna con la sua mappa»: non è più vero dal 27 agosto 2026 — la sezione ha una
    // mappa sola con le chip. Resta collassata per il numero di RIGHE, non più per il numero di mappe.
    private static readonly IReadOnlySet<string> InitiallyCollapsedKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "regulated" };

    /// <summary>Vero se la sezione si apre COLLASSATA nel documento: si espande a mano (doc 11 §3i).</summary>
    public static bool IsInitiallyCollapsed(string key) => InitiallyCollapsedKeys.Contains(key);

    // Sezioni derivate che NON si possono congelare: la loro derivazione è vera solo adesso. Un METAR catturato
    // al ciclo AIRAC non è un documento d'archivio, è meteo scaduto spacciato per attuale — quindi la sezione
    // non espone il toggle e la cattura frozen la salta.
    // ⚠️ «validity» sta qui per una ragione di ORDINE, non di gusto: il suo timbro parla della release, e la
    // cattura frozen gira DENTRO la creazione dello snapshot — quando quella release non esiste ancora. Non c'è
    // niente da congelare: si legge sempre dalla release che si sta mostrando.
    private static readonly IReadOnlySet<string> AlwaysLiveKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "weather", "validity" };

    /// <summary>Vero se la sezione si deriva SEMPRE dal vivo e non può essere congelata alla release.</summary>
    public static bool IsAlwaysLive(string key) => AlwaysLiveKeys.Contains(key);

    /// <summary>
    /// Vero se la sezione espone all'editor il toggle Live/Frozen (doc 10 §3a): le sezioni DERIVATE che non siano
    /// <see cref="IsAlwaysLive"/> — per quelle editoriali non esiste una derivazione da congelare, per quelle
    /// sempre-live congelarla sarebbe una bugia. La regola stava ripetuta identica nei tre editor (ACC, APP, vLOA)
    /// e vive qui, dove è definita la natura delle sezioni.
    /// </summary>
    public static bool IsRenderModeToggleable(string key) =>
        KindOf(key) == SectionKind.Derived && !IsAlwaysLive(key);

    // Corpo prodotto dalla PAGINA (doc 13 §3a): derivate + editoriali-strutturate. Scritto per esteso su ogni
    // voce perché non è deducibile dalla natura — «regulated» è un picker sulla vIPI ACC/APP e prosa sulla vLOA.
    // ⚠️ `en:` è il titolo INGLESE, e si scrive per esteso su ogni voce italiana: un titolo di catalogo non
    // passa mai dal traduttore automatico (non è un segmento del documento), quindi se non sta qui non esiste
    // in inglese da nessuna parte — e una vIPI d'aeroporto letta in inglese torna ad avere le testate in
    // italiano a copertura dichiarata completa. `en: null` vuol dire «uguale nelle due lingue» ed è una
    // risposta legittima solo per le SIGLE (AOR, SID, MRVA); ProfiloBilingueTests non accetta altro.
    private static SectionDescriptor D(string key, string title, int order,
                                       IReadOnlyList<SectionDescriptor>? children = null, string? en = null) =>
        new(key, title, order, KindOf(key), SectionBodySource.Blocks, children, en);

    private static SectionDescriptor H(string key, string title, int order,
                                       IReadOnlyList<SectionDescriptor>? children = null, string? en = null) =>
        new(key, title, order, KindOf(key), SectionBodySource.Host, children, en);

    /// <summary>Scheda dalla pagina IN TESTA, e sotto i blocchi editoriali della sezione.</summary>
    private static SectionDescriptor HB(string key, string title, int order,
                                        IReadOnlyList<SectionDescriptor>? children = null, string? en = null) =>
        new(key, title, order, KindOf(key), SectionBodySource.HostAndBlocks, children, en);

    // Membership per profilo (key, titolo, ordine). Universali a tutti: aor/frequencies/coordination/regulated/
    // operationaltechnique/validity. ACC/APP in italiano, vLOA in inglese (lettera di accordo bilaterale).
    // H(...) = corpo reso dalla pagina, D(...) = corpo dai blocchi della sezione.
    /// <summary>
    /// Le sezioni dell'APP non remotizzato. Estratto in un campo perché il profilo <b>militare</b> lo
    /// RIMANDA invece di ricopiarlo: due elenchi che devono restare uguali divergono, ed è già successo
    /// fra <c>VloaSections</c> e questo registro. Il giorno che il militare avrà sezioni sue, si separa —
    /// e sarà una scelta, non una svista.
    /// </summary>
    private static readonly IReadOnlyList<SectionDescriptor> Registry_App = new[]
    {
        H("separations", "Separazioni", 1, en: "Separations"),
        H("configurations", "Configurazioni", 2, en: "Configurations"),
        H("aor", "AOR", 3),
        H("frequencies", "Frequenze", 4, en: "Frequencies"),
        H("minima", "MRVA", 5),
        H("vfr", "VFR", 6),
        H("coordination", "Coordinamenti", 7, en: "Coordination"),
        H("regulated", "Aree regolamentate", 8, en: "Regulated areas"),
        D("operationaltechnique", "Procedure generali", 9, en: "General procedures"),
        HB("validity", "Validità e revisione", 10, en: "Validity and revision"),
    };

    private static readonly IReadOnlyDictionary<SectionProfile, IReadOnlyList<SectionDescriptor>> Registry =
        new Dictionary<SectionProfile, IReadOnlyList<SectionDescriptor>>
        {
            [SectionProfile.App] = Registry_App,
            [SectionProfile.AccAerovia] = new[]
            {
                H("separations", "Separazioni radar", 1, en: "Radar separation"),
                H("configurations", "Configurazioni", 2, en: "Configurations"),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4, en: "Frequencies"),
                H("minima", "MRVA", 5),
                H("coordination", "Coordinamenti", 7, en: "Coordination"),
                H("regulated", "Aree regolamentate", 8, en: "Regulated areas"),
                D("operationaltechnique", "Procedure generali", 9, en: "General procedures"),
                HB("validity", "Validità e revisione", 10, en: "Validity and revision"),
            },
            [SectionProfile.AccAppBlock] = new[]
            {
                H("separations", "Separazioni", 1, en: "Separations"),
                H("configurations", "Configurazioni", 2, en: "Configurations"),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4, en: "Frequencies"),
                H("minima", "MRVA", 5),
                H("vfr", "VFR", 6),
                H("coordination", "Coordinamenti", 7, en: "Coordination"),
                H("regulated", "Aree regolamentate", 8, en: "Regulated areas"),
                D("operationaltechnique", "Procedure generali", 9, en: "General procedures"),
                HB("validity", "Validità e revisione", 10, en: "Validity and revision"),
            },
            // vLOA: titoli e ORDINE sono quelli del documento reale (doc 13 §3c). Fino al doc 13 questo profilo non
            // lo leggeva nessuno — la struttura nasceva da VloaSections — e i due elenchi erano divergenti: mancava
            // «purpose», «General procedures» stava dopo «Coordination» e le aree si chiamavano «Regulated areas».
            [SectionProfile.Vloa] = new[]
            {
                D("purpose", "Purpose", 1),
                H("aor", "Areas of Responsibility", 2),
                H("frequencies", "Frequencies", 3),
                D("operationaltechnique", "General procedures", 4),
                H("coordination", "Coordination", 5),
                D("regulated", "Military areas coordination and management", 6),
                HB("validity", "Validity and Revision", 7),
            },
            // vIPI d'aeroporto (carta 2026-08-26). Le sei sezioni che c'erano già — con le stesse cose dentro —
            // più le due editoriali universali. Fuori restano «aor», «coordination» e «regulated»: l'aeroporto è un
            // LUOGO, e area di responsabilità e accordi appartengono alla torre e all'avvicinamento.
            // ⚠️ Titoli in italiano come App/Acc: il documento nasce `Language.It`. Le cotture di prima li
            // scrivevano in inglese, ed è per questo che il viewer aveva un heading inglese cablato.
            [SectionProfile.Airport] = new[]
            {
                H("weather", "METAR & TAF", 1),
                H("runwayrules", "Regole piste", 2, en: "Runway selection rules"),
                H("transition", "Quote di transizione", 3, en: "Transition altitude and levels"),
                H("frequencies", "Frequenze", 4, en: "Frequencies"),
                H("runways", "Piste", 5, en: "Runways"),
                H("sids", "SID", 6),
                D("operationaltechnique", "Procedure generali", 7, en: "General procedures"),
                HB("validity", "Validità e revisione", 8, en: "Validity and revision"),
            },

            // --- vSOP MILITARE d'aeroporto (carta 2026-08-27) ------------------------------------------
            //
            // VENTISEI sezioni tratte dai quindici SOP reali, che hanno TUTTI lo stesso indice: non e'
            // contenuto libero, e' un profilo. (Diceva «ventiquattro»: il conto era rimasto indietro di due
            // quando si sono aggiunte `qra` e `lowlevel`. Il numero vero lo conta
            // `ProfiloMilitareTests.Le_sezioni_sono_ventisei`, non questo commento.) Titoli in ITALIANO (§1d): la lingua sorgente e' quella in
            // cui si REDIGE, non quella dei PDF di partenza, e un lettore inglese lo ottiene tradotto.
            //
            // ⚠️ Le code per campo -- LVP di Pratica, SAR alert di Cervia, Combat departure di Gioia, il
            // Range LI-R59 di Decimomannu, l'HEMS di Pisa -- NON si seminano: sono sezioni libere, che il
            // catalogo gia' sa fare. Seminarne venticinque perche' un campo le ha tutte vorrebbe dire far
            // nascere quattordici documenti con roba da nascondere.
            [SectionProfile.AirportMil] = new[]
            {
                // ✚ Non e' nel PDF: meteo live, sempre-live, costo zero, nascondibile.
                H("weather", "METAR & TAF", 1),

                D("generaldata", "Dati generali", 2, en: "General data", children: new[]
                {
                    // Scheda + blocchi: la tabella in testa, e sotto la prosa che i quindici PDF hanno già.
                    HB("navaids", "Radioassistenze", 1, en: "Navigation aids"),
                    // Derivata: le posizioni IVAO dello scalo. Blocchi: CRC/GCI/AEW e l'APP di un altro
                    // campo, che il catalogo settori non ha. Sui campi militari i blocchi pesano PIU' della
                    // scheda -- misurato su LIPI Rivolto.
                    HB("frequencies", "Frequenze ATC/CRC", 2, en: "ATC/CRC frequencies"),
                    HB("diversion", "Aeroporti alternati", 3, en: "Diversion airfields"),
                    // Derivata: ident, lunghezza e QFU dall'anagrafica. Blocchi: le coordinate delle
                    // soglie, che AirportRunway non ha.
                    HB("runways", "Piste", 4, en: "Runways"),
                    // ✚ Non e' nel PDF: TA e tabella dei livelli per fascia QNH.
                    H("transition", "Quote di transizione", 5, en: "Transition altitude and levels"),
                    // Scheda + blocchi. ⚠️ Restano EDITORIALI: il contenuto è tutto nel payload, quindi la
                    // release lo fotografa già copiando i blocchi — non c'è nessuna derivazione da congelare.
                    HB("callsigns", "Nominativi", 6, en: "Callsigns"),
                    // ⚠️ IN CODA AI DATI GENERALI dal 3 settembre 2026, e prima stava in testa alle Procedure
                    // di terra. Richiesta del committente: i parcheggi sono un DATO dello scalo — un piazzale
                    // e i suoi stalli — non una procedura che si esegue, e stanno accanto a piste,
                    // radioassistenze e frequenze.
                    // ⚠️ Il catalogo decide la struttura solo alla NASCITA: i vSOP già scritti li sposta
                    // `IDocumentMaintenance.ReparentMilParkingsAsync`, perché a mano nessuno potrebbe — il
                    // motore di riordino sposta solo fra FRATELLI, apposta.
                    HB("parkings", "Parcheggi", 7, en: "Parking"),
                }),

                D("groundprocedures", "Procedure di terra", 3, en: "Ground procedures", children: new[]
                {
                    D("enginestart", "Messa in moto", 1, en: "Engine start"),
                    D("taxiing", "Rullaggio", 2, en: "Taxiing"),
                    D("arming", "Armamento/disarmo", 3, en: "Arming/de-arming"),
                }),

                D("flightprocedures", "Procedure di volo", 4, en: "Flight procedures", children: new[]
                {
                    D("takeoff", "Restrizioni al decollo", 1, en: "Take-off restrictions"),
                    D("sfo", "Circuito SFO/precauzionale", 2, en: "SFO/precautionary pattern"),
                    D("commfail", "Avaria comunicazioni", 3, en: "Radio failure"),
                    D("gca", "Circuito GCA", 4, en: "GCA pattern"),
                    D("vfrjet", "Porte e circuiti VFR jet", 5, en: "VFR jet gates and patterns"),
                    D("ifrsignificant", "Punti significativi strumentali", 6, en: "IFR significant points"),
                    D("gat", "Partenze/arrivi IFR GAT", 7, en: "GAT IFR departures/arrivals"),
                    // ⚠️ CONTENUTO NUOVO, non trascrizione: una sezione QRA/Scramble non esiste in nessuno
                    // dei quindici PDF -- QRA compare solo come colonna, e solo sulle quattro basi di
                    // difesa aerea (Amendola, Gioia, Istrana, Grosseto). Si semina su tutti perche'
                    // nascondere e' un clic; sugli altri undici campi nasce e si nasconde.
                    D("qra", "QRA / Scramble", 8),
                }),

                // La mappa AoR con le chip per area E' GIA' quello che il PDF disegna a mano, una figura
                // per volta: qui il riuso porta il motore, non solo la chiave.
                HB("regulated", "Aree di lavoro", 5, en: "Working areas", children: new[]
                {
                    D("operationaltechnique", "Procedure generali", 1, en: "General procedures"),
                    // Aree tattiche dove si vola il BOAT: parla di AREE, quindi sta sotto la sezione che le
                    // disegna. Presente in 9 SOP su 15.
                    D("lowlevel", "Bassa quota (BOAT)", 2, en: "Low level (BOAT)"),
                }),

                HB("validity", "Validità e revisione", 6, en: "Validity and revision"),
            },

            // vSOP militare di un APP non remotizzato: PER ORA le stesse sezioni del civile. Vedi sotto il
            // perche' si rimanda invece di ricopiare.
            [SectionProfile.AppMil] = Registry_App,
        };

    // Sezioni fisse che NON sono di primo livello: stanno fuori dal registro di membership, che descrive solo ciò
    // che si crea alla nascita del documento, ma sono fisse e rese dalla pagina come le altre. Il titolo è dinamico
    // (dipende dai codici della coppia), quindi qui non serve. Doc 13 §3c.
    private static readonly IReadOnlyDictionary<SectionProfile, IReadOnlyList<SectionDescriptor>> ChildRegistry =
        new Dictionary<SectionProfile, IReadOnlyList<SectionDescriptor>>
        {
            [SectionProfile.Vloa] = new[]
            {
                H(SectionKeys.CoordinationOut, "", 1),
                H(SectionKeys.CoordinationIn, "", 2),
            },
        };

    /// <summary>
    /// Vero se il corpo di questa sezione lo produce la PAGINA e non i blocchi della sezione (doc 13 §3a): sezioni
    /// derivate ed editoriali-strutturate. È la domanda che viewer ed editor si fanno per decidere se rendere il
    /// contenuto documentale o cedere il posto al componente dedicato — stava ripetuta in sei insiemi di pagina.
    /// </summary>
    public static bool IsHostRendered(SectionProfile profile, string key) =>
        Find(profile, key)?.BodySource is SectionBodySource.Host or SectionBodySource.HostAndBlocks;

    /// <summary>
    /// Vero se la sezione, oltre alla scheda che le disegna la pagina, tiene anche i PROPRI blocchi editoriali
    /// (<see cref="SectionBodySource.HostAndBlocks"/>). Chi rende una sezione host deve chiederlo: le altre i
    /// blocchi non li mostrano, e mostrarli tutti raddoppierebbe il corpo delle derivate.
    /// </summary>
    public static bool KeepsOwnBlocks(SectionProfile profile, string key) =>
        Find(profile, key)?.BodySource == SectionBodySource.HostAndBlocks;

    /// <summary>Profilo di catalogo di un blocco della vIPI ACC (Aerovia o gruppo APP): la corrispondenza stava
    /// scritta a mano nell'assembler e negli editor.</summary>
    public static SectionProfile ProfileOfAccBlock(AccBlockKind kind) =>
        kind == AccBlockKind.Aerovia ? SectionProfile.AccAerovia : SectionProfile.AccAppBlock;

    /// <summary>Sezioni fisse del profilo, in ordine di default.</summary>
    public static IReadOnlyList<SectionDescriptor> For(SectionProfile profile) => Registry[profile];

    /// <summary>
    /// Vero se i titoli di catalogo di questo profilo sono scritti in <b>inglese</b>: la vLOA, che è una
    /// lettera d'accordo bilaterale e nasce così.
    ///
    /// <para>⚠️ Non è un dettaglio di presentazione, è la lingua NATIVA del catalogo, e serve a chi risolve
    /// i titoli a view-time (<see cref="TitoliDiCatalogo"/>): per gli altri profili
    /// <see cref="SectionDescriptor.Title"/> è la resa italiana e <see cref="SectionDescriptor.TitleEn"/>
    /// quella inglese, qui <c>Title</c> è già l'inglese e <b>la resa italiana non esiste</b>. Trattarla come
    /// le altre vorrebbe dire imporre «Purpose» a chi legge in italiano, scavalcando la traduzione — che per
    /// una vLOA è l'unica cosa che quel titolo può tradurlo.</para>
    ///
    /// <para>⚠️ La stessa distinzione era scritta a mano dentro <c>CatalogoBilingueTests</c> (l'elenco dei
    /// «profili italiani»): due posti che dichiarano la stessa cosa sono due posti che possono
    /// contraddirsi, e il primo ad aggiungere un profilo se ne accorgerebbe solo a schermo.</para>
    /// </summary>
    public static bool TitoliInInglese(SectionProfile profile) => profile == SectionProfile.Vloa;

    /// <summary>Descrittore della sezione fissa con questa chiave: di primo livello o sotto-sezione fissa
    /// (<see cref="ChildRegistry"/>). Null = sezione libera.</summary>
    /// <summary>
    /// Il descrittore di catalogo di questa chiave, per questo profilo — <b>a qualunque profondità</b>.
    ///
    /// <para>
    /// ⚠️ <b>La ricerca scende nei figli dal 29 agosto 2026</b>, e prima no: guardava solo il primo livello
    /// del profilo più il <see cref="ChildRegistry"/>. Finché nessun profilo aveva sezioni annidate la
    /// differenza non esisteva; il vSOP militare ne ha venti su ventisei, e su quelle <c>Find</c> rispondeva
    /// <c>null</c>. Da lì: <see cref="IsHostRendered"/> falso su <c>frequencies</c>, <c>runways</c> e
    /// <c>transition</c> — che sono <b>rese dalla pagina</b> — e <see cref="IsFixed"/> falso su tutte e venti,
    /// cioè venti sezioni di CATALOGO scambiate per sezioni libere.
    /// </para>
    /// <para>
    /// ⚠️ A schermo si vedeva così: nel vSOP militare pubblicato, «Frequenze ATC/CRC», «Piste» e «Quote di
    /// transizione» uscivano come <b>titoli vuoti</b>. Nessun test lo prendeva perché tutte le altre famiglie
    /// hanno le derivate al primo livello.
    /// </para>
    /// <para>Misurato: gli unici descrittori con figli sono i quattro contenitori di <c>AirportMil</c>, quindi
    /// la discesa non cambia una virgola per gli altri profili.</para>
    /// </summary>
    public static SectionDescriptor? Find(SectionProfile profile, string key) =>
        Cerca(For(profile), key)
        ?? (ChildRegistry.TryGetValue(profile, out var children) ? Cerca(children, key) : null);

    private static SectionDescriptor? Cerca(IEnumerable<SectionDescriptor> descrittori, string key)
    {
        foreach (var d in descrittori)
        {
            if (string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)) return d;
            if (d.Children is { Count: > 0 } figli && Cerca(figli, key) is { } trovato) return trovato;
        }
        return null;
    }

    public static bool IsFixed(SectionProfile profile, string key) => Find(profile, key) is not null;
}
