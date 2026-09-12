using Vipi.Domain;

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
            ["lvp"] = SectionKind.Derived,            // minimi di bassa visibilita' (carta 2026-09-12)
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
            ["lowlevel"] = SectionKind.Editorial,

            // --- l'indice chiesto dal SOD (6 settembre 2026) ------------------------------------------
            // Editoriali: sono prosa, immagini e tabelle scritte a mano — nessun catalogo nostro sa dire
            // dove passa il rullaggio su un piazzale o che cosa restringe un circuito.
            [SectionKeys.AirportLayout] = SectionKind.Editorial,
            [SectionKeys.ApronFlow] = SectionKind.Editorial,
            [SectionKeys.ArrivalRestrictions] = SectionKind.Editorial,
            [SectionKeys.CircuitRestrictions] = SectionKind.Editorial,
            [SectionKeys.VfrJetPoints] = SectionKind.Editorial,
            [SectionKeys.DepartureProcedures] = SectionKind.Editorial,
            [SectionKeys.DepartureProceduresVfr] = SectionKind.Editorial,
            [SectionKeys.DepartureProceduresIfr] = SectionKind.Editorial,
            [SectionKeys.ArrivalProcedures] = SectionKind.Editorial,
            [SectionKeys.ArrivalProceduresVfr] = SectionKind.Editorial,
            [SectionKeys.ArrivalProceduresIfr] = SectionKind.Editorial,
            // ⚠️ EDITORIALE benche' la tabella sia palesemente derivata, e non e' una svista: la
            // derivazione e' quella di «runways» (AirportSectionProjection.Runways), e la release la
            // congela LI', sotto quella chiave (AirportViewDerivationService: frozen.Get("runways")).
            // Dichiararla Derived prometterebbe un secondo congelamento che nessuno esegue -- e darebbe
            // DUE interruttori Live/Frozen sulla stessa tabella, che possono contraddirsi: la stessa
            // pista fotografata a due cicli diversi, una sopra l'altra. Il corpo lo disegna comunque la
            // pagina: quello lo dice `SectionBodySource`, non `SectionKind`.
            [SectionKeys.RunwayThresholds] = SectionKind.Editorial,

            // --- Carte aeroportuali (3 settembre 2026), su vIPI d'aeroporto e vSOP militare -----------
            // Editoriali: il contenuto sono immagini e allegati PDF, che i blocchi sanno già portare. Non
            // c'è niente da derivare — le carte non stanno in nessun catalogo nostro.
            ["charts"] = SectionKind.Editorial,
            ["charts:aerodrome"] = SectionKind.Editorial,
            ["charts:iac"] = SectionKind.Editorial,
            ["charts:sid"] = SectionKind.Editorial,
            ["charts:star"] = SectionKind.Editorial,
            ["charts:vfr"] = SectionKind.Editorial,
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
    // ⚠️ `aud:` e' il pubblico con cui la sezione NASCE (indice del SOD, 6 settembre 2026), non un permesso:
    // chi scrive lo cambia dall'editor e da li' in poi decide il documento. Assente = «per tutti».
    private static SectionDescriptor D(string key, string title, int order,
                                       IReadOnlyList<SectionDescriptor>? children = null, string? en = null,
                                       SectionAudience aud = SectionAudience.Both) =>
        new(key, title, order, KindOf(key), SectionBodySource.Blocks, children, en, aud);

    private static SectionDescriptor H(string key, string title, int order,
                                       IReadOnlyList<SectionDescriptor>? children = null, string? en = null,
                                       SectionAudience aud = SectionAudience.Both) =>
        new(key, title, order, KindOf(key), SectionBodySource.Host, children, en, aud);

    /// <summary>Scheda dalla pagina IN TESTA, e sotto i blocchi editoriali della sezione.</summary>
    private static SectionDescriptor HB(string key, string title, int order,
                                        IReadOnlyList<SectionDescriptor>? children = null, string? en = null,
                                        SectionAudience aud = SectionAudience.Both) =>
        new(key, title, order, KindOf(key), SectionBodySource.HostAndBlocks, children, en, aud);

    /// <summary>Scorciatoia di lettura per le sezioni che il SOD marca «[PILOTS]».</summary>
    private const SectionAudience Piloti = SectionAudience.Pilots;

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

    /// <summary>
    /// Le carte dello scalo (3 settembre 2026, richiesta del committente): un contenitore e cinque raccolte,
    /// <b>prima</b> di «Validità e revisione».
    ///
    /// <para>⚠️ Scritte QUI e non due volte: la vIPI d'aeroporto e il vSOP militare descrivono lo stesso
    /// luogo, e due elenchi copiati sarebbero due elenchi diversi al primo ritocco — è il difetto già pagato
    /// dal profilo <c>AppMil</c>, che infatti <b>rimanda</b> a quello civile invece di ricopiarlo.</para>
    ///
    /// <para>⚠️ Le chiavi sono <c>charts:*</c> e non <c>sids</c>/<c>vfr</c>: quelle due hanno già un mestiere
    /// (le SID importate, la sezione VFR di un profilo di posizione), e dentro un profilo una chiave compare
    /// una volta sola.</para>
    /// </summary>
    private static IReadOnlyList<SectionDescriptor> CarteAeroportuali(int ordine) =>
        new[]
        {
            D(SectionKeys.Charts, "Carte aeroportuali", ordine, en: "Airport charts", children: new[]
            {
                D(SectionKeys.ChartsAerodrome, "Aerodromo", 1, en: "Aerodrome"),
                D(SectionKeys.ChartsIac, "Carte di avvicinamento strumentale", 2, en: "Instrument approach charts"),
                // Sigle: uguali nelle due lingue, come AOR e MRVA — tradurle sarebbe un errore, non una
                // gentilezza (docs/design/regole-lingua.md).
                D(SectionKeys.ChartsSid, "SID", 3),
                D(SectionKeys.ChartsStar, "STAR", 4),
                D(SectionKeys.ChartsVfr, "VFR", 5),
            }),
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
                H("transition", "Quote di transizione", 2, en: "Transition altitude and levels"),
                H("frequencies", "Frequenze", 3, en: "Frequencies"),
                // ⚠️ Le REGOLE PISTE sono FIGLIE delle Piste dal 12 settembre 2026 (sera, committente): una
                // regola dice quale pista si usa, quindi sta con le piste e non da un'altra parte
                // dell'indice. Prima erano una sezione di primo livello, la seconda del documento.
                // ⚠️ Nel vSOP militare restano SORELLE di «Piste» (§CX, decisione dell'11 settembre): i due
                // indici divergono qui, ed è voluto — quello militare lo detta il SOD.
                // ⚠️ Nei documenti GIÀ SCRITTI il catalogo non le sposta: la struttura la decide alla
                // NASCITA. Lo fa `IDocumentMaintenance.ReparentAirportSectionsAsync` all'avvio.
                H("runways", "Piste", 4, en: "Runways", children: new[]
                {
                    H("runwayrules", "Regole piste", 1, en: "Runway selection rules"),
                }),
                H("sids", "SID", 5),
                D("operationaltechnique", "Procedure generali", 6, en: "General procedures"),
                // ✚ Non c'era (12 settembre 2026, committente): i minimi di bassa visibilita'. Sta SUBITO
                // DOPO le «Procedure generali» — sorella, NON figlia: le LVP sono un modo di operare, e
                // vengono dopo la prosa che descrive come si opera. Come le regole piste, il dato sta
                // nell'ANAGRAFICA dello scalo: qui c'è solo la porta.
                H("lvp", "LVP", 7),
            }.Concat(CarteAeroportuali(8)).Append(
                HB("validity", "Validità e revisione", 9, en: "Validity and revision")).ToArray(),

            // --- vSOP MILITARE d'aeroporto (carta 2026-08-27) ------------------------------------------
            //
            // Le sezioni sono tratte dai quindici SOP reali, che hanno TUTTI lo stesso indice: non e'
            // contenuto libero, e' un profilo. Dal 6 settembre 2026 l'indice e' quello chiesto dal SOD
            // (carta 2026-09-06-vsop-sezioni-sod.md): dodici sezioni in piu' e QRA fuori.
            // ⚠️ Il numero vero lo conta `ProfiloMilitareTests`, non questo commento -- che ha gia'
            // detto «ventiquattro» quando erano ventisei. Titoli in ITALIANO (§1d): la lingua sorgente e' quella in
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
                    HB("diversion", "Aeroporti alternati", 3, en: "Diversion airfields", aud: Piloti),
                    // Derivata: ident, lunghezza e QFU dall'anagrafica. Le coordinate delle SOGLIE
                    // stavano qui dentro, come seconda tabella; dal 6 settembre 2026 sono una
                    // sotto-sezione loro (indice del SOD).
                    // La riga diceva «le coordinate delle soglie, che AirportRunway non ha»: non e' piu'
                    // vero dal 30 agosto 2026 -- ThresholdLat/Lon/ElevationFt arrivano da IVAO insieme
                    // alle piste, ed e' per questo che la sotto-sezione e' resa dalla PAGINA e non scritta
                    // a mano.
                    HB("runways", "Piste", 4, en: "Runways", children: new[]
                    {
                        H(SectionKeys.RunwayThresholds, "Coordinate delle soglie", 1,
                          en: "Threshold coordinates", aud: Piloti),
                    }),
                    // ✚ Non e' nel PDF (11 settembre 2026, committente): le regole di scelta pista, SUBITO
                    // DOPO le Piste. Come le SID stanno nell'ANAGRAFICA dello scalo (`AirportRunwayRules`),
                    // non nel documento: la vIPI civile e' solo la porta di scrittura, e su un campo senza
                    // vIPI civile la porta e' l'editor di questo vSOP. Dalle regole il viewer marca la pista
                    // IN USO adesso, nelle Piste e nelle SID — la stessa derivazione della vIPI.
                    // ⚠️ Sorella e non figlia di «Piste», come le SID: stessa scelta, stesso indice leggibile.
                    H("runwayrules", "Regole piste", 5, en: "Runway selection rules"),
                    // ✚ Non e' nel PDF (12 settembre 2026, committente): i minimi LVP, subito dopo le regole
                    // piste come nella vIPI civile. Stesso dato, stessa chiave, stessa porta di scrittura —
                    // che qui e' l'editor del vSOP solo se il campo non ha una vIPI civile (§AS).
                    // ⚠️ La nota in testa a questo profilo diceva che le code per campo, «LVP di Pratica»
                    // compresa, restano sezioni LIBERE: da oggi vale per tutto TRANNE i minimi, che sono un
                    // dato dello scalo. La prosa di Pratica resta libera, o va nella nota dei minimi.
                    H("lvp", "LVP", 6),
                    // Derivata, come la sorella civile: le SID stanno nell'ANAGRAFICA dello scalo
                    // (`AirportSids`, importate dal sectorfile), non nel documento — la vIPI civile e' solo
                    // la porta di SCRITTURA. Quindi qui non c'e' nessun ramo da fare: il vSOP legge
                    // dall'aeroporto, misto o solo militare che sia.
                    // ⚠️ Sorella di «Piste» e non figlia: una scelta di indice, decisa dal committente.
                    // ⚠️ `H` e non `HB`: derivata pura, senza blocchi. Le code per campo -- il «Combat
                    // departure» di Gioia -- restano sezioni LIBERE, come dice la nota in testa al profilo.
                    H("sids", "SID", 7),
                    // ✚ Non e' nel PDF: TA e tabella dei livelli per fascia QNH.
                    H("transition", "Quote di transizione", 8, en: "Transition altitude and levels"),
                    // Scheda + blocchi. ⚠️ Restano EDITORIALI: il contenuto è tutto nel payload, quindi la
                    // release lo fotografa già copiando i blocchi — non c'è nessuna derivazione da congelare.
                    HB("callsigns", "Nominativi", 9, en: "Callsigns", aud: Piloti),
                    // ⚠️ IN CODA AI DATI GENERALI dal 3 settembre 2026, e prima stava in testa alle Procedure
                    // di terra. Richiesta del committente: i parcheggi sono un DATO dello scalo — un piazzale
                    // e i suoi stalli — non una procedura che si esegue, e stanno accanto a piste,
                    // radioassistenze e frequenze.
                    // ⚠️ Il catalogo decide la struttura solo alla NASCITA: i vSOP già scritti li sposta
                    // `IDocumentMaintenance.ReparentMilParkingsAsync`, perché a mano nessuno potrebbe — il
                    // motore di riordino sposta solo fra FRATELLI, apposta.
                    // NON e' la carta d'aerodromo, che sta in «Carte aeroportuali»: quella e' un allegato,
                    // questa e' la descrizione dello scalo che i SOP scrivono a parole.
                    D(SectionKeys.AirportLayout, "Planimetria dell'aeroporto", 10, en: "Airport layout"),
                    // ⚠️ I parcheggi CHIUDONO i dati generali, ed e' una decisione del 3 settembre 2026 che
                    // il SOD conferma: la sua planimetria viene prima. Un test lo pretende -- e ha gia'
                    // fermato questa modifica una volta, quando la planimetria era finita in coda.
                    HB("parkings", "Parcheggi", 11, en: "Parking", aud: Piloti, children: new[]
                    {
                        D(SectionKeys.ApronFlow, "Flusso di rullaggio sui piazzali", 1,
                          en: "Aprons taxi flow", aud: Piloti),
                    }),
                }),

                D("groundprocedures", "Procedure di terra", 3, en: "Ground procedures", children: new[]
                {
                    D("enginestart", "Messa in moto", 1, en: "Engine start", aud: Piloti),
                    D("taxiing", "Rullaggio", 2, en: "Taxiing"),
                    D("arming", "Armamento/disarmo", 3, en: "Arming/de-arming", aud: Piloti),
                }),

                // ⚠️ L'ORDINE e' quello del SOD (6 settembre 2026) e non quello dei PDF: le
                // restrizioni stanno insieme in testa -- decollo, arrivo, circuito -- e dopo vengono i
                // circuiti particolari e i punti.
                // ⚠️ Qui c'era «QRA / Scramble», l'unica sezione che avevamo INVENTATO noi: non sta in
                // nessuno dei quindici PDF e il SOD non la vuole. Dai documenti gia' scritti la toglie
                // `IDocumentMaintenance.RemoveMilQraSectionsAsync`, che se dentro c'e' del testo la
                // trasforma in sezione libera invece di buttarla via.
                D("flightprocedures", "Procedure di volo", 4, en: "Flight procedures", children: new[]
                {
                    D("takeoff", "Restrizioni al decollo", 1, en: "Take-off restrictions", aud: Piloti),
                    D(SectionKeys.ArrivalRestrictions, "Restrizioni all'arrivo", 2,
                      en: "Arrival restrictions", aud: Piloti),
                    D(SectionKeys.CircuitRestrictions, "Restrizioni di circuito", 3,
                      en: "Circuit restrictions"),
                    D("sfo", "Circuito SFO/precauzionale", 4, en: "SFO/precautionary pattern"),
                    D("commfail", "Avaria comunicazioni", 5, en: "Radio failure"),
                    D("gca", "Circuito GCA", 6, en: "GCA pattern"),
                    // ⚠️ I punti VFR stanno SOTTO le porte VFR jet, i punti IFR restano una sezione a
                    // se': e' l'asimmetria che il SOD ha chiesto, non una svista da «uniformare».
                    D("vfrjet", "Porte e circuiti VFR jet", 7, en: "VFR jet gates and patterns", children: new[]
                    {
                        D(SectionKeys.VfrJetPoints, "Punti significativi VFR", 1,
                          en: "VFR significant points", aud: Piloti),
                    }),
                    D("ifrsignificant", "Punti significativi strumentali", 8,
                      en: "IFR significant points", aud: Piloti),
                    D("gat", "Partenze/arrivi IFR GAT", 9, en: "GAT IFR departures/arrivals"),
                }),

                // La mappa AoR con le chip per area E' GIA' quello che il PDF disegna a mano, una figura
                // per volta: qui il riuso porta il motore, non solo la chiave.
                HB("regulated", "Aree di lavoro", 5, en: "Working areas", children: new[]
                {
                    // ⚠️ «operationaltechnique» e' una chiave UNIVERSALE (sta in ACC, APP, vLOA e
                    // aeroporto): questi quattro discendenti vivono nel SOLO registro militare, perche'
                    // `Children` e' per profilo. Lo pretende un test -- gli altri quattro documenti non
                    // devono vedersi comparire delle procedure di partenza.
                    // ⚠️ PROFONDITA' 3, cioe' il LIMITE (DocumentSection.MaxDepth):
                    // «arrivalprocedures:vfr» sta esatto sul bordo. Chi volesse annidare sotto queste non
                    // puo', e deve saperlo prima di provarci -- alla nascita sarebbe un'eccezione, non un
                    // documento storto.
                    D("operationaltechnique", "Procedure generali", 1, en: "General procedures", children: new[]
                    {
                        D(SectionKeys.DepartureProcedures, "Procedure di partenza", 1,
                          en: "Departure procedures", children: new[]
                        {
                            D(SectionKeys.DepartureProceduresVfr, "VFR", 1),
                            D(SectionKeys.DepartureProceduresIfr, "IFR", 2),
                        }),
                        D(SectionKeys.ArrivalProcedures, "Procedure di arrivo", 2,
                          en: "Arrival procedures", children: new[]
                        {
                            D(SectionKeys.ArrivalProceduresVfr, "VFR", 1),
                            D(SectionKeys.ArrivalProceduresIfr, "IFR", 2),
                        }),
                    }),
                    // Aree tattiche dove si vola il BOAT: parla di AREE, quindi sta sotto la sezione che le
                    // disegna. Presente in 9 SOP su 15.
                    // ⚠️ `HB` e non `D` dal 9 settembre 2026 (carta 2026-09-09-aree-boat.md): ha un
                    // visualizzatore SUO — mappa, elenco e tabella — gemello di quello del padre, e SOTTO
                    // restano i blocchi editoriali di chi ha gia' scritto prosa li'. Il payload e' lo stesso
                    // `MilRegulatedPayload`, sotto questa chiave di sezione: nessuna tabella nuova.
                    // ⚠️ `SectionKind` resta editoriale, come per «regulated»: non c'e' nessuna derivazione
                    // da congelare alla release, e prometterne una darebbe un interruttore Live/Frozen che
                    // nessuno esegue.
                    HB("lowlevel", "Bassa quota (BOAT)", 2, en: "Low level (BOAT)", aud: Piloti),
                }),
            }.Concat(CarteAeroportuali(6)).Append(
                HB("validity", "Validità e revisione", 7, en: "Validity and revision")).ToArray(),

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
