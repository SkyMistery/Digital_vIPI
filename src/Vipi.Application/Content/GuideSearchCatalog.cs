using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Catalogo statico delle sezioni della pagina Guida (<c>/services/vsop/guide</c>), per farle emergere nella ricerca
/// globale: cercare "come pubblico" / "lock" deve portare alla sezione giusta della guida, non solo ai documenti.
/// NON è un documento (nessuna entità/tabella): è un piccolo indice in-memory che <see cref="SearchService"/>
/// fonde con i risultati del repo. Tenere gli <c>Anchor</c> allineati agli <c>Id</c> di <c>GuidaPage.razor</c>.
/// </summary>
public static class GuideSearchCatalog
{
    /// <summary>
    /// Voce indicizzabile: ancora di sezione, titolo e estratto <b>nelle due lingue</b>, e le parole chiave.
    ///
    /// <para>⚠️ <b>Le parole chiave sono UNA lista sola, e ci stanno dentro tutte e due le lingue.</b> Non si
    /// sdoppiano per lingua, e la ragione è d'uso: su un sito che si legge in inglese qualcuno cercherà
    /// «frequenze», e su uno italiano qualcuno cercherà «runway». Chi cerca vuole trovare, non essere
    /// coerente.</para>
    ///
    /// <para>⚠️ L'inglese qui è scritto <b>a mano</b>, come tutte le stringhe dell'applicazione
    /// (<c>docs/design/regole-lingua.md</c> R7): al motore automatico va solo la prosa dei documenti.</para>
    /// </summary>
    public sealed record Entry(string Anchor, string TitleIt, string TitleEn, string Keywords,
                               string SnippetIt, string SnippetEn)
    {
        /// <summary>Il titolo nella lingua di chi legge.</summary>
        public string Title(bool inglese) => inglese ? TitleEn : TitleIt;

        /// <summary>L'estratto nella lingua di chi legge.</summary>
        public string Snippet(bool inglese) => inglese ? SnippetEn : SnippetIt;
    }

    public static readonly IReadOnlyList<Entry> Entries = new[]
    {
        new Entry("nav", "Navigare tra le pagine", "Navigating the pages",
            "naviga navigazione pagine home acc menu landing barra topbar navigate navigation pages",
            "Come muoversi tra Home, landing ACC, documenti (vIPI, aeroporti, APP, vLOA).",
            "How to move between Home, ACC landings and documents (vIPI, airports, APP, vLOA)."),
        new Entry("ricerca", "Cercare", "Searching",
            "ricerca cerca search full-text cop fix callsign frequenze search find",
            "La barra di ricerca full-text: CoP, FIX, callsign, frequenze e testo dei documenti.",
            "The full-text search bar: CoPs, fixes, callsigns, frequencies and the text of documents."),
        new Entry("live", "Stato live e ATC online", "Live status and ATC online",
            "live online atc connesso disconnesso badge callsign unicom connected disconnected",
            "Il badge live, il conteggio ATC online e la risoluzione in tempo reale.",
            "The live badge, the online ATC count and real-time resolution."),
        new Entry("changed", "Cosa è cambiato", "What has changed",
            "cambiato cambiamenti novita changed aggiornato nuovo airac ciclo changes news updated new",
            "Documenti nuovi o aggiornati nel ciclo AIRAC corrente.",
            "Documents that are new or updated in the current AIRAC cycle."),
        new Entry("anteprime", "Anteprime e badge bozza", "Previews and draft badges",
            "anteprima anteprime bozza draft release congelata as pubblica preview previews draft frozen",
            "Vedere un documento come pubblico, bozza o release congelata (parametro ?as=).",
            "Seeing a document as public, as a draft or as a frozen release (the ?as= parameter)."),
        new Entry("aree", "Leggere le aree regolamentate", "Reading the regulated areas",
            "aree area regolamentata regolamentate special area zona zone militari R D P TSA TRA restricted danger prohibited chip pastiglia mappa 2d 3d filtro tipo colore quota banda descrizione areas regulated restricted danger prohibited map filter type colour",
            "La mappa unica delle aree regolamentate: le pastiglie, i filtri per tipo, i colori e le descrizioni.",
            "The single map of regulated areas: the chips, the filters by type, the colours and the descriptions."),
        new Entry("editor-accesso", "Chi può modificare e da dove", "Who can edit, and from where",
            "modificare permesso permessi grant admin editor incarichi accesso edit permission permissions access",
            "Chi può editare, il tasto Editor, l'hub documenti e gli Incarichi.",
            "Who can edit, the Editor button, the documents hub and Tasks."),
        new Entry("versioni", "Elenco documenti, versioni e release", "Documents, versions and releases",
            "versioni versione elenco documenti hub bozza bozze release airac pubblica pubblicare programma ciclo nascondi nascosto elimina eliminare scarta storia modifiche differenze anteprima lock sblocco filtro filtri chip versions list documents hub draft drafts hide delete history differences",
            "L'hub documenti: l'elenco con i filtri che contano, le versioni, la storia e le release AIRAC.",
            "The documents hub: the list with the filters that matter, versions, history and AIRAC releases."),
        new Entry("editor-lock", "Prendere in carico (lock)", "Taking a page over (lock)",
            "lock blocco inizia modifica sola lettura heartbeat sblocco forza start editing read only unlock force",
            "Prendere in carico una pagina in sola lettura: il lock, l'heartbeat, forzare lo sblocco.",
            "Taking over a read-only page: the lock, the heartbeat, forcing the unlock."),
        new Entry("editor-documento", "Editor documento: sezioni e blocchi", "Document editor: sections and blocks",
            "editor documento sezioni blocchi aggiungi riordina rinomina elimina derivate editor sections blocks add reorder rename delete derived",
            "Come si compone un documento: sezioni e blocchi, riordino, sezioni derivate.",
            "How a document is put together: sections and blocks, reordering, derived sections."),
        new Entry("editor-blocchi", "I tipi di blocco", "Block types",
            "blocco blocchi prosa testo tabella callout tip separazioni mappa aor immagine immagini foto fotografia carica caricare upload trascina drag drop png jpeg webp gif didascalia testo alternativo block blocks prose text table callout image images photo upload caption alt text",
            "I tipi di blocco disponibili: prosa, tabella, callout, immagine (dal dispositivo o trascinata), separazioni, mappa AOR.",
            "The block types available: prose, table, callout, image (from your device or dragged in), separations, AOR map."),
        new Entry("editor-salva", "Indice laterale e salvataggio", "Side index and saving",
            "salva salvataggio salva tutto toc indice laterale dirty ctrl s bozza save saving toc index dirty",
            "L'indice laterale, il salvataggio nella bozza e le sezioni non salvate.",
            "The side index, saving into the draft and the sections not yet saved."),
        new Entry("editor-anteprima", "Vedere la bozza in anteprima", "Previewing the draft",
            "anteprima bozza draft controlla prima pubblicare preview draft check before publishing",
            "Aprire la bozza come apparirà, prima di pubblicare.",
            "Opening the draft as it will look, before publishing."),
        new Entry("editor-release", "Pubblicare (release AIRAC)", "Publishing (AIRAC release)",
            "pubblicare pubblica pubblicazione release airac ciclo pubblica ora programma snapshot nota differenze annulla publish publishing release schedule snapshot note differences cancel",
            "Rendere pubblica la bozza con una release AIRAC: pubblica ora o programma al ciclo.",
            "Making the draft public with an AIRAC release: publish now or schedule it for the cycle."),
        new Entry("editor-validita", "Validità e revisione", "Validity and revision",
            "validita validità revisione ciclo airac appartenenza data in vigore dal effective from revisione reviewed by chi ha pubblicato firmatario nome vid posizione staff timbro non ancora pubblicato non registrato review cycle signatory validity revision effective from reviewed by signatory review cycle",
            "I tre campi che il documento scrive da sé quando lo pubblichi: ciclo AIRAC, data e chi ha premuto Pubblica.",
            "The three fields the document writes by itself when you publish: AIRAC cycle, date and who pressed Publish."),
        new Entry("editor-aeroporto", "Editor aeroporto (SID, frequenze, piste)", "Airport editor (SIDs, frequencies, runways)",
            "aeroporto editor sid frequenze piste runway settori import aurora modificate salva selezione scegli pubblica chip pista verificare larghezza piena tastiera doppio clic shift scala categoria wtc stesso punto stessa transition applica alle scelte ctrl invio propaga airport editor runway runways frequencies sectors import apply keyboard",
            "L'editor del profilo aeroporto: piste, frequenze, SID e settori ATC.",
            "The airport profile editor: runways, frequencies, SIDs and ATC sectors."),
        new Entry("editor-app", "Editor APP: come si scrive", "APP editor: how it is written",
            "editor app avvicinamento non remotizzato sezioni derivate blocco blocchi espandi comprimi tutto nascosta larghezza piena bozza release airac anteprima separazioni configurazioni aor frequenze vfr coordinamenti aree regolamentate approach non remotised derived expand collapse hidden full width",
            "L'editor dell'APP non remotizzato: sezioni derivate, blocchi, espandi/comprimi, bozza e release.",
            "The non-remotised APP editor: derived sections, blocks, expand and collapse, draft and release."),
        new Entry("editor-vloa", "Editor vLOA: come si scrive", "vLOA editor: how it is written",
            "editor vloa loa coppia home neighbour vicino estero inglese sezioni derivate aree frequenze coordinamenti chip mappa elenco settori nel documento blocco espandi comprimi bozza release airac letter of agreement home neighbour foreign english derived",
            "L'editor della vLOA: lato Home, sezioni derivate dai due ACC, quali settori entrano nel documento.",
            "The vLOA editor: the Home side, the sections derived from the two ACCs, which sectors go into the document."),
        new Entry("editor-mil", "Editor vSOP militare", "Military vSOP editor",
            "militare militari mil vsop sop aeronautica base campo qra scramble boat bassa quota aree di lavoro destinatario pilota atc chip italiano traduzione inglese aviano ghedi rivolto decimomannu istrana grosseto amendola gioia del colle military base audience pilot atc translation english",
            "L'editor del vSOP militare d'aeroporto: italiano, destinatario per sezione, aree di lavoro, release.",
            "The airport military vSOP editor: written in Italian, audience per section, working areas, release."),
        new Entry("editor-acc", "Editor vIPI ACC", "ACC vIPI editor",
            "acc vipi editor blocchi blocco gruppo app aerovia comprimi espandi fisarmonica sezioni larghezza piena lock bozza settori blocks group airway expand collapse accordion sections",
            "L'editor del vIPI di ACC: i blocchi che si aprono uno per volta, le sezioni, la bozza.",
            "The ACC vIPI editor: the blocks that open one at a time, the sections, the draft."),
        new Entry("editor-frequenze", "Frequenze (derivate)", "Frequencies (derived)",
            "frequenze frequenza derivate riordina trascina collega link callsign mhz albero settori frequency derived reorder drag link callsign sector tree",
            "Le frequenze derivate dall'albero dei settori: riordinarle e collegarne di esterne.",
            "The frequencies derived from the sector tree: reordering them and linking external ones."),
        new Entry("editor-configurazioni", "Configurazioni di settore", "Sector configurations",
            "configurazione configurazioni assetto settori aperti accorpamento unificato center point range configuration configurations open sectors merging unified center point range",
            "Le configurazioni di apertura dei settori e la tabella di accorpamento che ne deriva.",
            "The sector opening configurations, and the merging table that follows from them."),
        // ⚠️ «MRVA» in chiaro fra le parole chiave: la sezione si chiama così in tutte e due le lingue
        // (regole-lingua, le sigle non si traducono), ma chi cerca scrive anche «minime» o «vettoramento».
        new Entry("editor-minime", "Minime di vettoramento", "Minimum vectoring altitudes",
            "minime minima vettoramento mrva mva carta carte altitudine quota settore a mano manuale import sectorfile minimum vectoring altitude chart charts by hand manual",
            "La sezione delle minime di vettoramento (MRVA): si compila a mano, e perché non arriva dall'import.",
            "The minimum vectoring altitudes section (MRVA): filled in by hand, and why it does not come from the import."),
        new Entry("editor-aor", "AOR: shape extra e colori", "AOR: extra shapes and colours",
            "aor area responsabilita shape extra colori colore settore torre estero mappa anelli area of responsibility shape extra colour colours sector tower foreign map rings",
            "La mappa AOR: aggiungere shape di altri enti e cambiare i colori per settore.",
            "The AOR map: adding shapes from other units and changing the colours per sector."),
        new Entry("accordi", "Accordi di coordinamento", "Coordination agreements",
            "accordo accordi coordinamento coordinamenti trasferimenti trasferimento clausola clausole punto punti cop quota livello verso versi bilaterale variante varianti alternativa eccezione incolla tabella lacune proposte ricevente mittente aeroporti riceve da prossimo da chi cediamo chi riceviamo direzione agreement agreements coordination transfer transfers clause clauses point points level direction bilateral variant exception paste table gaps receiving sending",
            "Gli accordi di coordinamento: due lati, più aeroporti, clausole con più punti, due versi; incolla-tabella e cruscotto delle lacune; come si legge dal lato di chi riceve.",
            "Coordination agreements: two sides, several airports, clauses with several points, two directions; paste-a-table and the gaps dashboard; how it reads from the receiving side."),
        new Entry("struttura", "Struttura: la gerarchia di copertura", "Structure: the coverage hierarchy",
            "struttura gerarchia copertura fallback padre padri albero settore settori aeroporto app risalita ereditato scaletta agganciare orfano orfani posizioni aeroporto trascina structure hierarchy coverage fallback parent tree sector airport orphan orphans drag",
            "L'albero di fallback unico della divisione: chi copre chi, il padre di ogni nodo, gli orfani da agganciare.",
            "The single fallback tree of the division: who covers whom, the parent of each node, the orphans to attach."),
        new Entry("admin-aeroporti", "Aeroporti: assegnazione alle ACC", "Airports: assigning them to ACCs",
            "aeroporti aeroporto assegna assegnazione assegnare acc competenza anagrafica ivao auto-assegna re-import reimport piste transition altitude nascosto nascondi settori twr torre genera documenti elimina sposta gruppo selezione airports assign assignment auto assign reimport runways hidden generate documents move bulk",
            "La pagina Aeroporti: assegnare gli aeroporti IVAO alle ACC, auto-assegna, re-import, azioni di gruppo e stati (nascosti, senza settori, senza TWR).",
            "The Airports page: assigning IVAO airports to ACCs, auto-assign, re-import, bulk actions and states (hidden, without sectors, without TWR)."),
        new Entry("admin-confinanti", "Confinanti: coppie e vLOA", "Neighbours: pairs and vLOAs",
            "confinanti confinante vicino vicini estero esteri vloa coppia coppie adiacenza adiacente soglia poligono shape import calcola conferma rifiuta genera settore estero paese paesi frontiera neighbours neighbour foreign pair pairs adjacency threshold polygon import confirm reject generate country border",
            "La pagina Confinanti: le coppie ACC italiano ↔ estero, la verifica dell'adiacenza e la generazione della vLOA.",
            "The Neighbours page: the Italian ACC to foreign ACC pairs, the adjacency check and generating the vLOA."),
        new Entry("admin-permessi", "Permessi: chi può modificare cosa", "Permissions: who can edit what",
            "permesso permessi livello livelli ruolo ruoli vid staff staffista promuovi promuovere promozione declassa declassare togliere pavimento socio redattore amministratore editor admin division staff chi puo modificare permission permissions level levels role promote demote floor member editor administrator",
            "La pagina Permessi: una riga per persona, il suo livello, il pavimento che gli dà la posizione staff, e come promuovere o togliere.",
            "The Permissions page: one row per person, their level, the floor their staff position grants, and how to promote or remove."),
        new Entry("admin-sorgenti", "Sorgenti: cosa arriva da fuori", "Sources: what comes from outside",
            "sorgenti sorgente import importa importato manuale policy ivao sola lettura bloccato escludi esclusa categoria transition altitude piste settori sid aree regolamentate congelate reimport automatico giro fermo stantio provenienza sources source import manual policy read only excluded frozen automatic stale",
            "La pagina Sorgenti: quali dati arrivano da IVAO e quali si gestiscono a mano, e come sta ogni import.",
            "The Sources page: which data comes from IVAO and which is kept by hand, and how each import is doing."),
        new Entry("nuovo-documento", "Nuovo documento: cosa crea e cosa apre", "New document: what it creates and what it opens",
            "nuovo documento creare crea crea documento apri editor vloa vipi acc app aeroporto coppia home neighbour estero duplicata gia esiste permesso grant inizia modifica lock sezioni obbligatorie new document create open editor pair duplicate already exists mandatory sections",
            "La pagina Nuovo documento: la vLOA si crea qui, le vIPI le crea il loro editor.",
            "The New document page: a vLOA is created here, the vIPIs are created by their own editor."),
        new Entry("revisioni", "Documenti da rivedere", "Documents to review",
            "rivedere revisione revisioni segnalazione segnalazioni impatto impatti banner pastiglia ripubblicare ripubblica deriva stantio non piu elencato rinominato rinomina orfano orfani settore sparito nascosto area cambiata rotto bersaglio gia in pubblico review notices impact banner chip republish stale renamed orphan sector gone hidden area changed broken",
            "Le segnalazioni sui documenti: che cosa e cambiato a monte, quali vanno rivisti o ripubblicati, e perche alcune righe non hanno il segno di spunta.",
            "The notices on documents: what changed upstream, which ones have to be reviewed or republished, and why some rows have no tick."),
        new Entry("admin-diagnostica", "Diagnostica: cosa non torna", "Diagnostics: what does not add up",
            "diagnostica diagnosi incongruenza incongruenze rilievo rilievi soft-ref orfano orfana dangling schema drift colonna mancante sql_mode strict mode max_allowed_packet avvio manutenzione admin amministratore nessuno callsign ambiguo immagini orfane spazio pulizia salute health diagnostics inconsistency orphan dangling schema drift missing column startup maintenance ambiguous callsign orphan images health",
            "La pagina Diagnostica: cosa non torna, in cinque aree, e dove si ripara.",
            "The Diagnostics page: what does not add up, in five areas, and where it is repaired."),
        new Entry("incarichi", "I miei incarichi", "My tasks",
            "incarichi incarico compito compiti task assegnato assegnati mio miei da fare in corso in revisione fatto bloccato scadenza airac ritardo promemoria personale colonna colonne stato avanzamento tasks task assigned mine to do in progress in review done blocked deadline late reminder column state",
            "I miei incarichi: le tre colonne del lavoro in corso, il passo successivo, le scadenze AIRAC.",
            "My tasks: the three columns of the work in progress, the next step, the AIRAC deadlines."),
        new Entry("admin-incarichi", "Incarichi: chi sta facendo cosa", "Tasks: who is doing what",
            "incarichi incarico assegna assegnare assegnatario riassegna riassegnazione priorita scadenza airac ritardo stato avanzamento editor staffista elimina lavoro editoriale chi sta facendo cosa non conclusi tasks assign assignee reassign priority deadline late state progress editorial work",
            "La pagina Incarichi admin: assegnare il lavoro, seguirlo per persona e per stato, riassegnare.",
            "The admin Tasks page: assigning the work, following it by person and by state, reassigning."),
        new Entry("admin-audit", "Audit: chi ha fatto cosa", "Audit: who did what",
            "audit registro log chi ha fatto cosa tracciamento traccia eliminato eliminazione nascosto permesso revoca gerarchia lock forzato sbloccato pubblicazione storico cronologia audit register log tracking deleted deletion hidden permission revoke hierarchy forced lock unlocked history",
            "Il registro degli atti amministrativi: pubblicazioni, eliminazioni, permessi, gerarchia, lock forzati.",
            "The register of administrative acts: publications, deletions, permissions, hierarchy, forced locks."),
        new Entry("profile-swapper", "Aurora Profile Swapper", "Aurora Profile Swapper",
            "aurora profilo profili cpr swapper scambia scambio copia copiare sezione sezioni trafficlists zip destinazione sorgente incolla configurazione radar profile profiles swap copy section sections destination source paste radar configuration",
            "Copiare sezioni intere fra profili Aurora .cpr: sorgente, destinazioni, anteprima e zip.",
            "Copying whole sections between Aurora .cpr profiles: source, destinations, preview and zip."),
        // ⚠️ Questa voce esisteva già, ma puntava a un'ancora che nella Guida NON c'era: cercare
        // «statistiche» dava un risultato che portava a una pagina senza quel capitolo. Il capitolo è stato
        // scritto il 25 agosto 2026 — una voce di ricerca senza la sua sezione è peggio di nessuna voce.
        new Entry("convertitore-coordinate", "Convertitore di coordinate", "Coordinate converter",
            "coordinate coordinata convertire conversione convertitore formato formati dms gradi primi secondi decimali sectorfile aurora restrict geo db ivao lat lon latitudine longitudine kml kmz google earth arinc poligono anello area punti vertici mappa perimetro coordinates convert conversion converter format degrees minutes seconds decimal database latitude longitude polygon ring points vertices map perimeter",
            "Coordinate in qualsiasi formato riscritte per il DB di IVAO o per il sectorfile, con la mappa.",
            "Coordinates in any format rewritten for the IVAO database or the sectorfile, with the map."),
        new Entry("statistiche", "Statistiche ATC", "ATC statistics",
            "statistiche statistica ore movimenti traffico gestito turni sessioni connessioni classifica divisione quanto ho controllato aerei presenze mie personali quando controlli costanza settimane aeroporti gestiti visti copertura coperto scoperto vid cerca controllore periodo utc statistics hours movements traffic handled shifts sessions connections leaderboard division coverage covered uncovered",
            "Le mie ore e il traffico gestito, il dettaglio di una sessione, la classifica e la copertura di divisione.",
            "My hours and the traffic handled, the detail of one session, the leaderboard and the coverage of the division."),
        new Entry("admin", "Aree admin", "Admin areas",
            "admin gerarchia settori trasferimenti sorgenti permessi audit import admin hierarchy sectors transfers sources permissions audit import",
            "Le pagine admin: gerarchia, trasferimenti, sorgenti, permessi, audit.",
            "The admin pages: hierarchy, transfers, sources, permissions, audit."),
    };

    /// <summary>
    /// Voci che corrispondono alla query, ordinate per numero di token combacianti (più pertinenti prima).
    /// Match: la query intera contenuta nell'haystack, oppure un token (≥3 caratteri) contenuto.
    /// </summary>
    public static IEnumerable<Entry> Match(string query)
    {
        var q = (query ?? "").Trim().ToLowerInvariant();
        if (q.Length < 2) yield break;
        var tokens = q.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(t => t.Length >= 3).ToArray();

        var scored = new List<(Entry e, int score)>();
        foreach (var e in Entries)
        {
            // ⚠️ Il pagliaio porta ENTRAMBE le lingue: chi legge in inglese e cerca «frequenze» deve
            // trovare lo stesso, e viceversa. È la stessa ragione per cui le parole chiave non si sdoppiano.
            var title = (e.TitleIt + " " + e.TitleEn).ToLowerInvariant();
            var hay = (title + " " + e.Keywords + " " + e.SnippetIt + " " + e.SnippetEn).ToLowerInvariant();
            var score = 0;
            if (hay.Contains(q)) score += 2;                     // frase intera → più forte
            score += tokens.Count(t => hay.Contains(t));         // token singoli, ovunque
            if (title.Contains(q)) score += 3;                   // match nel titolo = sezione dedicata → pesa di più
            score += tokens.Count(t => title.Contains(t)) * 2;   // token nel titolo valgono doppio
            if (score > 0) scored.Add((e, score));
        }
        foreach (var (e, _) in scored.OrderByDescending(x => x.score))
            yield return e;
    }

    /// <summary>Costruisce un <see cref="SearchHit"/> per una voce Guida (deep-link all'ancora di sezione).</summary>
    /// <summary>
    /// Il risultato di ricerca, nella lingua di chi legge.
    /// <para>⚠️ La lingua non si deduce qui: la passa il chiamante, che è l'unico a sapere chi sta leggendo
    /// (<c>ReadingLanguageContext</c>). È la regola di tutta la prosa generata dal codice — si sceglie il
    /// TESTO GIUSTO, non si traduce l'uscita.</para>
    /// </summary>
    public static SearchHit ToHit(Entry e, bool inglese) => new()
    {
        DocTitle = e.Title(inglese),
        DocType = DocumentType.Vipi,   // campo non usato in rendering/filtri: gli hit Guida vivono solo in scope All
        Where = (inglese ? "Guide › " : "Guida › ") + e.Title(inglese),
        Snippet = e.Snippet(inglese),
        Url = "/services/vsop/guide#" + e.Anchor,
    };
}
