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
            ["navaids"] = SectionKind.Editorial,
            ["diversion"] = SectionKind.Editorial,
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
    private static SectionDescriptor D(string key, string title, int order,
                                       IReadOnlyList<SectionDescriptor>? children = null) =>
        new(key, title, order, KindOf(key), SectionBodySource.Blocks, children);

    private static SectionDescriptor H(string key, string title, int order,
                                       IReadOnlyList<SectionDescriptor>? children = null) =>
        new(key, title, order, KindOf(key), SectionBodySource.Host, children);

    /// <summary>Scheda dalla pagina IN TESTA, e sotto i blocchi editoriali della sezione.</summary>
    private static SectionDescriptor HB(string key, string title, int order,
                                        IReadOnlyList<SectionDescriptor>? children = null) =>
        new(key, title, order, KindOf(key), SectionBodySource.HostAndBlocks, children);

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
        H("separations", "Separazioni", 1),
        H("configurations", "Configurazioni", 2),
        H("aor", "AOR", 3),
        H("frequencies", "Frequenze", 4),
        H("minima", "Minime di vettoramento", 5),
        H("vfr", "VFR", 6),
        H("coordination", "Coordinamenti", 7),
        H("regulated", "Aree regolamentate", 8),
        D("operationaltechnique", "Procedure generali", 9),
        HB("validity", "Validità e revisione", 10),
    };

    private static readonly IReadOnlyDictionary<SectionProfile, IReadOnlyList<SectionDescriptor>> Registry =
        new Dictionary<SectionProfile, IReadOnlyList<SectionDescriptor>>
        {
            [SectionProfile.App] = Registry_App,
            [SectionProfile.AccAerovia] = new[]
            {
                H("separations", "Separazioni radar", 1),
                H("configurations", "Configurazioni", 2),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4),
                H("minima", "Minime di vettoramento", 5),
                H("coordination", "Coordinamenti", 7),
                H("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                HB("validity", "Validità e revisione", 10),
            },
            [SectionProfile.AccAppBlock] = new[]
            {
                H("separations", "Separazioni", 1),
                H("configurations", "Configurazioni", 2),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4),
                H("minima", "Minime di vettoramento", 5),
                H("vfr", "VFR", 6),
                H("coordination", "Coordinamenti", 7),
                H("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                HB("validity", "Validità e revisione", 10),
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
                H("runwayrules", "Regole piste", 2),
                H("transition", "Quote di transizione", 3),
                H("frequencies", "Frequenze", 4),
                H("runways", "Piste", 5),
                H("sids", "SID", 6),
                D("operationaltechnique", "Procedure generali", 7),
                HB("validity", "Validità e revisione", 8),
            },

            // --- vSOP MILITARE d'aeroporto (carta 2026-08-27) ------------------------------------------
            //
            // Ventiquattro sezioni tratte dai quindici SOP reali, che hanno TUTTI lo stesso indice: non e'
            // contenuto libero, e' un profilo. Titoli in ITALIANO (§1d): la lingua sorgente e' quella in
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

                D("generaldata", "Dati generali", 2, new[]
                {
                    D("navaids", "Radioassistenze", 1),
                    // Derivata: le posizioni IVAO dello scalo. Blocchi: CRC/GCI/AEW e l'APP di un altro
                    // campo, che il catalogo settori non ha. Sui campi militari i blocchi pesano PIU' della
                    // scheda -- misurato su LIPI Rivolto.
                    HB("frequencies", "Frequenze ATC/CRC", 2),
                    D("diversion", "Aeroporti alternati", 3),
                    // Derivata: ident, lunghezza e QFU dall'anagrafica. Blocchi: le coordinate delle
                    // soglie, che AirportRunway non ha.
                    HB("runways", "Piste", 4),
                    // ✚ Non e' nel PDF: TA e tabella dei livelli per fascia QNH.
                    H("transition", "Quote di transizione", 5),
                    D("callsigns", "Nominativi", 6),
                }),

                D("groundprocedures", "Procedure di terra", 3, new[]
                {
                    D("parkings", "Parcheggi", 1),
                    D("enginestart", "Messa in moto", 2),
                    D("taxiing", "Rullaggio", 3),
                    D("arming", "Armamento/disarmo", 4),
                }),

                D("flightprocedures", "Procedure di volo", 4, new[]
                {
                    D("takeoff", "Restrizioni al decollo", 1),
                    D("sfo", "Circuito SFO/precauzionale", 2),
                    D("commfail", "Avaria comunicazioni", 3),
                    D("gca", "Circuito GCA", 4),
                    D("vfrjet", "Porte e circuiti VFR jet", 5),
                    D("ifrsignificant", "Punti significativi strumentali", 6),
                    D("gat", "Partenze/arrivi IFR GAT", 7),
                    // ⚠️ CONTENUTO NUOVO, non trascrizione: una sezione QRA/Scramble non esiste in nessuno
                    // dei quindici PDF -- QRA compare solo come colonna, e solo sulle quattro basi di
                    // difesa aerea (Amendola, Gioia, Istrana, Grosseto). Si semina su tutti perche'
                    // nascondere e' un clic; sugli altri undici campi nasce e si nasconde.
                    D("qra", "QRA / Scramble", 8),
                }),

                // La mappa AoR con le chip per area E' GIA' quello che il PDF disegna a mano, una figura
                // per volta: qui il riuso porta il motore, non solo la chiave.
                HB("regulated", "Aree di lavoro", 5, new[]
                {
                    D("operationaltechnique", "Procedure generali", 1),
                    // Aree tattiche dove si vola il BOAT: parla di AREE, quindi sta sotto la sezione che le
                    // disegna. Presente in 9 SOP su 15.
                    D("lowlevel", "Bassa quota (BOAT)", 2),
                }),

                HB("validity", "Validità e revisione", 6),
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

    /// <summary>Descrittore della sezione fissa con questa chiave: di primo livello o sotto-sezione fissa
    /// (<see cref="ChildRegistry"/>). Null = sezione libera.</summary>
    public static SectionDescriptor? Find(SectionProfile profile, string key) =>
        For(profile).FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? (ChildRegistry.TryGetValue(profile, out var children)
            ? children.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase))
            : null);

    public static bool IsFixed(SectionProfile profile, string key) => Find(profile, key) is not null;
}
