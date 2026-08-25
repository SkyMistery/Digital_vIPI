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
    /// <summary>Voce indicizzabile: ancora di sezione, titolo, parole chiave e un estratto per il risultato.</summary>
    public sealed record Entry(string Anchor, string Title, string Keywords, string Snippet);

    public static readonly IReadOnlyList<Entry> Entries = new[]
    {
        new Entry("nav", "Navigare tra le pagine", "naviga navigazione pagine home acc menu landing barra topbar", "Come muoversi tra Home, landing ACC, documenti (vIPI, aeroporti, APP, vLOA)."),
        new Entry("ricerca", "Cercare", "ricerca cerca search full-text cop fix callsign frequenze", "La barra di ricerca full-text: CoP, FIX, callsign, frequenze e testo dei documenti."),
        new Entry("live", "Stato live e ATC online", "live online atc connesso disconnesso badge callsign unicom", "Il badge live, il conteggio ATC online e la risoluzione in tempo reale."),
        new Entry("changed", "Cosa è cambiato", "cambiato cambiamenti novita changed aggiornato nuovo airac ciclo", "Documenti nuovi o aggiornati nel ciclo AIRAC corrente."),
        new Entry("anteprime", "Anteprime e badge bozza", "anteprima anteprime bozza draft release congelata as pubblica", "Vedere un documento come pubblico, bozza o release congelata (parametro ?as=)."),
        new Entry("editor-accesso", "Chi può modificare e da dove", "modificare permesso permessi grant admin editor incarichi accesso", "Chi può editare, il tasto Editor, l'hub documenti e gli Incarichi."),
        new Entry("versioni", "Elenco documenti, versioni e release", "versioni versione elenco documenti hub bozza bozze release airac pubblica pubblicare programma ciclo nascondi nascosto elimina eliminare scarta storia modifiche differenze anteprima lock sblocco filtro filtri chip", "L'hub documenti: l'elenco con i filtri che contano, le versioni, la storia e le release AIRAC."),
        new Entry("editor-lock", "Prendere in carico (lock)", "lock blocco inizia modifica sola lettura heartbeat sblocco forza", "Prendere in carico una pagina in sola lettura: il lock, l'heartbeat, forzare lo sblocco."),
        new Entry("editor-documento", "Editor documento: sezioni e blocchi", "editor documento sezioni blocchi aggiungi riordina rinomina elimina derivate", "Come si compone un documento: sezioni e blocchi, riordino, sezioni derivate."),
        new Entry("editor-blocchi", "I tipi di blocco", "blocco blocchi prosa testo tabella callout tip separazioni mappa aor immagine immagini foto fotografia carica caricare upload trascina drag drop png jpeg webp gif didascalia testo alternativo", "I tipi di blocco disponibili: prosa, tabella, callout, immagine (dal dispositivo o trascinata), separazioni, mappa AOR."),
        new Entry("editor-salva", "Indice laterale e salvataggio", "salva salvataggio salva tutto toc indice laterale dirty ctrl s bozza", "L'indice laterale, il salvataggio nella bozza e le sezioni non salvate."),
        new Entry("editor-anteprima", "Vedere la bozza in anteprima", "anteprima bozza draft controlla prima pubblicare", "Aprire la bozza come apparirà, prima di pubblicare."),
        new Entry("editor-release", "Pubblicare (release AIRAC)", "pubblicare pubblica pubblicazione release airac ciclo pubblica ora programma snapshot nota differenze annulla", "Rendere pubblica la bozza con una release AIRAC: pubblica ora o programma al ciclo."),
        new Entry("editor-aeroporto", "Editor aeroporto (SID, frequenze, piste)", "aeroporto editor sid frequenze piste runway settori import aurora modificate salva selezione scegli pubblica chip pista verificare larghezza piena tastiera doppio clic shift scala categoria wtc stesso punto stessa transition applica alle scelte ctrl invio propaga", "L'editor del profilo aeroporto: piste, frequenze, SID e settori ATC."),
        new Entry("editor-app", "Editor APP: come si scrive", "editor app avvicinamento non remotizzato sezioni derivate blocco blocchi espandi comprimi tutto nascosta larghezza piena bozza release airac anteprima separazioni configurazioni aor frequenze vfr coordinamenti aree regolamentate", "L'editor dell'APP non remotizzato: sezioni derivate, blocchi, espandi/comprimi, bozza e release."),
        new Entry("editor-vloa", "Editor vLOA: come si scrive", "editor vloa loa coppia home neighbour vicino estero inglese sezioni derivate aree frequenze coordinamenti chip mappa elenco settori nel documento blocco espandi comprimi bozza release airac", "L'editor della vLOA: lato Home, sezioni derivate dai due ACC, quali settori entrano nel documento."),
        new Entry("editor-acc", "Editor vIPI ACC", "acc vipi editor blocchi blocco gruppo app aerovia comprimi espandi fisarmonica sezioni larghezza piena lock bozza settori", "L'editor del vIPI di ACC: i blocchi che si aprono uno per volta, le sezioni, la bozza."),
        new Entry("editor-frequenze", "Frequenze (derivate)", "frequenze frequenza derivate riordina trascina collega link callsign mhz albero settori", "Le frequenze derivate dall'albero dei settori: riordinarle e collegarne di esterne."),
        new Entry("editor-configurazioni", "Configurazioni di settore", "configurazione configurazioni assetto settori aperti accorpamento unificato center point range", "Le configurazioni di apertura dei settori e la tabella di accorpamento che ne deriva."),
        new Entry("editor-aor", "AOR: shape extra e colori", "aor area responsabilita shape extra colori colore settore torre estero mappa anelli", "La mappa AOR: aggiungere shape di altri enti e cambiare i colori per settore."),
        new Entry("accordi", "Accordi di coordinamento", "accordo accordi coordinamento coordinamenti trasferimenti trasferimento clausola clausole punto punti cop quota livello verso versi bilaterale variante varianti alternativa eccezione incolla tabella lacune proposte ricevente mittente aeroporti riceve da prossimo da chi cediamo chi riceviamo direzione", "Gli accordi di coordinamento: due lati, più aeroporti, clausole con più punti, due versi; incolla-tabella e cruscotto delle lacune; come si legge dal lato di chi riceve."),
        new Entry("struttura", "Struttura: la gerarchia di copertura", "struttura gerarchia copertura fallback padre padri albero settore settori aeroporto app risalita ereditato scaletta agganciare orfano orfani posizioni aeroporto trascina", "L'albero di fallback unico della divisione: chi copre chi, il padre di ogni nodo, gli orfani da agganciare."),
        new Entry("admin-aeroporti", "Aeroporti: assegnazione alle ACC", "aeroporti aeroporto assegna assegnazione assegnare acc competenza anagrafica ivao auto-assegna re-import reimport piste transition altitude nascosto nascondi settori twr torre genera documenti elimina sposta gruppo selezione", "La pagina Aeroporti: assegnare gli aeroporti IVAO alle ACC, auto-assegna, re-import, azioni di gruppo e stati (nascosti, senza settori, senza TWR)."),
        new Entry("admin-confinanti", "Confinanti: coppie e vLOA", "confinanti confinante vicino vicini estero esteri vloa coppia coppie adiacenza adiacente soglia poligono shape import calcola conferma rifiuta genera settore estero paese paesi frontiera", "La pagina Confinanti: le coppie ACC italiano ↔ estero, la verifica dell'adiacenza e la generazione della vLOA."),
        new Entry("admin-permessi", "Permessi: chi può modificare cosa", "permesso permessi grant vid staff staffista concedi concessione revoca revocare togliere acc editor admin abilitare abilitazione chi puo modificare", "La pagina Permessi: una riga per persona, i suoi ACC, come concedere e come revocare."),
        new Entry("admin-sorgenti", "Sorgenti: cosa arriva da fuori", "sorgenti sorgente import importa importato manuale policy ivao sola lettura bloccato escludi esclusa categoria transition altitude piste settori sid aree regolamentate congelate reimport automatico giro fermo stantio provenienza", "La pagina Sorgenti: quali dati arrivano da IVAO e quali si gestiscono a mano, e come sta ogni import."),
        new Entry("nuovo-documento", "Nuovo documento: cosa crea e cosa apre", "nuovo documento creare crea crea documento apri editor vloa vipi acc app aeroporto coppia home neighbour estero duplicata gia esiste permesso grant inizia modifica lock sezioni obbligatorie", "La pagina Nuovo documento: la vLOA si crea qui, le vIPI le crea il loro editor."),
        new Entry("revisioni", "Documenti da rivedere", "rivedere revisione revisioni segnalazione segnalazioni impatto impatti banner pastiglia ripubblicare ripubblica deriva stantio non piu elencato rinominato rinomina orfano orfani settore sparito nascosto area cambiata rotto bersaglio gia in pubblico", "Le segnalazioni sui documenti: che cosa e cambiato a monte, quali vanno rivisti o ripubblicati, e perche alcune righe non hanno il segno di spunta."),
        new Entry("admin-diagnostica", "Diagnostica: cosa non torna", "diagnostica diagnosi incongruenza incongruenze rilievo rilievi soft-ref orfano orfana dangling schema drift colonna mancante sql_mode strict mode max_allowed_packet avvio manutenzione admin amministratore nessuno callsign ambiguo immagini orfane spazio pulizia salute health", "La pagina Diagnostica: cosa non torna, in cinque aree, e dove si ripara."),
        new Entry("incarichi", "I miei incarichi", "incarichi incarico compito compiti task assegnato assegnati mio miei da fare in corso in revisione fatto bloccato scadenza airac ritardo promemoria personale colonna colonne stato avanzamento", "I miei incarichi: le tre colonne del lavoro in corso, il passo successivo, le scadenze AIRAC."),
        new Entry("admin-incarichi", "Incarichi: chi sta facendo cosa", "incarichi incarico assegna assegnare assegnatario riassegna riassegnazione priorita scadenza airac ritardo stato avanzamento editor staffista elimina lavoro editoriale chi sta facendo cosa non conclusi", "La pagina Incarichi admin: assegnare il lavoro, seguirlo per persona e per stato, riassegnare."),
        new Entry("admin-audit", "Audit: chi ha fatto cosa", "audit registro log chi ha fatto cosa tracciamento traccia eliminato eliminazione nascosto permesso revoca gerarchia lock forzato sbloccato pubblicazione storico cronologia", "Il registro degli atti amministrativi: pubblicazioni, eliminazioni, permessi, gerarchia, lock forzati."),
        new Entry("profile-swapper", "Aurora Profile Swapper", "aurora profilo profili cpr swapper scambia scambio copia copiare sezione sezioni trafficlists zip destinazione sorgente incolla configurazione radar", "Copiare sezioni intere fra profili Aurora .cpr: sorgente, destinazioni, anteprima e zip."),
        // ⚠️ Questa voce esisteva già, ma puntava a un'ancora che nella Guida NON c'era: cercare
        // «statistiche» dava un risultato che portava a una pagina senza quel capitolo. Il capitolo è stato
        // scritto il 25 agosto 2026 — una voce di ricerca senza la sua sezione è peggio di nessuna voce.
        new Entry("statistiche", "Statistiche ATC", "statistiche statistica ore movimenti traffico gestito turni sessioni connessioni classifica divisione quanto ho controllato aerei presenze mie personali quando controlli costanza settimane aeroporti gestiti visti copertura coperto scoperto vid cerca controllore periodo utc", "Le mie ore e il traffico gestito, il dettaglio di una sessione, la classifica e la copertura di divisione."),
        new Entry("admin", "Aree admin", "admin gerarchia settori trasferimenti sorgenti permessi audit import", "Le pagine admin: gerarchia, trasferimenti, sorgenti, permessi, audit."),
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
            var title = e.Title.ToLowerInvariant();
            var hay = (title + " " + e.Keywords + " " + e.Snippet).ToLowerInvariant();
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
    public static SearchHit ToHit(Entry e) => new()
    {
        DocTitle = e.Title,
        DocType = DocumentType.Vipi,   // campo non usato in rendering/filtri: gli hit Guida vivono solo in scope All
        Where = "Guida › " + e.Title,
        Snippet = e.Snippet,
        Url = "/services/vsop/guide#" + e.Anchor,
    };
}
