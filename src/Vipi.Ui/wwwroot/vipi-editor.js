// Aiutanti JS degli editor vIPI: fisarmonica dei blocchi, espandi/comprimi, ancore, store locale, barra markdown.
(function () {
    // ⚠️ Qui stavano le scorciatoie Ctrl/Cmd+E/Z/Y (`vipiEditorInit`, che registrava un DotNetObjectReference
    // per chiamare `ToggleEdit`/`UndoAction`/`RedoAction`). Nessuna pagina le registrava più, e nessun
    // componente ha quei metodi: il listener girava a vuoto su ogni tasto (T-071, 13 settembre 2026).

    // ⚠️ Qui stavano il Ctrl/Cmd+S dell'editor aeroporto («salva le sezioni modificate»), la guardia
    // `beforeunload` e i due ganci che le servivano (`vipiAirportEditorInit`, `vipiSetDirty`). Sono caduti
    // il 4 settembre 2026 con la cosa che proteggevano: quell'editor non accumula più niente, ogni gesto
    // scrive (carta 2026-09-04-aeroporto-porta-sola). Una guardia che non ha niente da guardare insegna
    // solo a ignorare gli avvisi.
    // Fisarmonica dei BLOCCHI dell'editor ACC: aprendone uno gli altri si chiudono. Senza, la pagina torna
    // subito ai 9 690px misurati — e con due o tre gruppi APP aperti l'indice non basta più a orientarsi.
    // `toggle` NON fa bubbling: va ascoltato in cattura (stessa ragione di vipi-aor.js).
    // Il listener è installato UNA volta: questo file può essere rieseguito da una navigazione arricchita, e
    // due listener chiuderebbero i fratelli due volte.
    if (!window.__vipiAccAccordion) {
        window.__vipiAccAccordion = true;
        document.addEventListener('toggle', function (ev) {
            var d = ev.target;
            if (!d || !d.matches || !d.matches('details.acc-block')) return;
            // Apertura fatta da «Espandi tutto»: consuma il marchio e lascia stare i fratelli. ⚠️ Il `toggle`
            // arriva DOPO (è messo in coda), quindi una bandiera spenta in fondo alla funzione di gruppo
            // sarebbe già spenta quando l'evento arriva — misurato: «espandi tutto» ne apriva uno solo.
            if (d.__vipiBulk) { d.__vipiBulk = false; return; }
            if (!d.open) return;
            var parent = d.parentElement;
            if (!parent) return;
            // Chiudere un fratello che sta SOPRA accorcia la pagina sotto il puntatore: il blocco appena aperto
            // scivola in su. Misurato saltandoci da una voce dell'indice: la sezione bersaglio finiva a −249px,
            // cioè fuori schermo di sopra. Si compensa con uno scrollBy della differenza, come fa vipiDetails.
            var prima = d.getBoundingClientRect().top;
            parent.querySelectorAll(':scope > details.acc-block').forEach(function (other) {
                if (other !== d) { other.open = false; }
            });
            var dopo = d.getBoundingClientRect().top;
            if (dopo !== prima) window.scrollBy(0, dopo - prima);
        }, true);
    }

    // Espande/comprime tutto l'editor: le sezioni bespoke dell'aeroporto (details.ed-sec), i blocchi della vIPI
    // ACC e le card di sezione condivise (CollapsibleBlock → details.cb). Un helper solo: due farebbero due
    // comportamenti diversi per lo stesso tasto. Le aperture di gruppo sono MARCHIATE, altrimenti la
    // fisarmonica richiuderebbe subito i blocchi appena aperti.
    window.vipiEditorSections = function (open) {
        var want = !!open;
        document.querySelectorAll('details.ed-sec, .ed-layout details.acc-block, .ed-layout details.cb').forEach(function (d) {
            if (d.open === want) return;       // già com'è: nessun toggle, nessun marchio da consumare
            d.__vipiBulk = true;               // un marchio = un evento, quindi niente bandiere che restano su
            if (want) { d.setAttribute('open', ''); } else { d.removeAttribute('open'); }
        });
    };

    if (!window.__vipiEditorAnchors) {
        window.__vipiEditorAnchors = true;
        // Saltando a una sezione dalla mini-nav (#sec-…), se è collassata la apre.
        window.addEventListener('hashchange', function () {
            if (!location.hash) return;
            var el = document.querySelector(location.hash);
            if (el && el.tagName === 'DETAILS') el.setAttribute('open', '');
        });
    }

    // Store chiave/valore su localStorage (best-effort: ignora quota/privacy errori).
    window.vipiStoreGet = function (key) {
        try { return window.localStorage.getItem(key); } catch (e) { return null; }
    };
    window.vipiStoreSet = function (key, val) {
        try { if (val == null) window.localStorage.removeItem(key); else window.localStorage.setItem(key, val); } catch (e) { }
    };

    // ---- Barra di formattazione delle textarea markdown (RichTextArea.razor) -----------------------
    //
    // ⚠️ Ogni gesto finisce con un `change` SINTETICO. Blazor ascolta `onchange` con un listener delegato
    // sul documento: scrivere `el.value` da JS non fa scattare nessun evento da solo, e senza questa riga
    // il testo cambierebbe a schermo e NON tornerebbe mai nel modello — sparirebbe al primo re-render.
    function vipiMdFine(el, s, e) {
        el.focus();
        el.selectionStart = s;
        el.selectionEnd = e;
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Avvolge la selezione, o la sguscia se è già avvolta (il tasto è un interruttore, come in un editor
    // vero: premuto due volte torna indietro invece di impilare i marcatori).
    window.vipiMdWrap = function (el, pre, post) {
        if (!el) return;
        var s = el.selectionStart, e = el.selectionEnd, v = el.value;
        var dentro = v.substring(s, e);

        // Già avvolta DENTRO la selezione (**testo**) o FUORI (il cursore sta fra i marcatori)?
        if (dentro.length > pre.length + post.length &&
            dentro.slice(0, pre.length) === pre && dentro.slice(-post.length) === post) {
            var nudo = dentro.slice(pre.length, dentro.length - post.length);
            el.value = v.substring(0, s) + nudo + v.substring(e);
            return vipiMdFine(el, s, s + nudo.length);
        }
        if (v.substring(s - pre.length, s) === pre && v.substring(e, e + post.length) === post) {
            el.value = v.substring(0, s - pre.length) + dentro + v.substring(e + post.length);
            return vipiMdFine(el, s - pre.length, s - pre.length + dentro.length);
        }

        el.value = v.substring(0, s) + pre + dentro + post + v.substring(e);
        // Selezione vuota: il cursore va FRA i marcatori, pronto a scrivere. Con la selezione, resta sul
        // testo — così si può incalzare con un secondo tasto (grassetto E corsivo) senza riselezionare.
        vipiMdFine(el, s + pre.length, s + pre.length + dentro.length);
    };

    // ---- SID citata nel testo (§A73, 18 settembre 2026) ------------------------------------------------
    //
    // Due tempi, perché fra il tasto e la scelta il fuoco se ne va: il selettore ha un SUO campo di ricerca.
    //   1. `vipiSidPrendi(contenitore)`, al clic sul tasto: segna il campo di destinazione e ci scrive la
    //      selezione. Il campo è il contenitore stesso (la textarea della prosa) oppure quello che, dentro il
    //      contenitore, ha il fuoco (la cella della tabella). ⚠️ Il tasto ha `@onmousedown:preventDefault`: il
    //      fuoco resta nel campo fino al clic, ed è per questo che qui c'è ancora.
    //   2. `vipiSidInserisci(testo)`, alla scelta: scrive nel campo segnato, alla selezione salvata, e chiude
    //      col `change` SINTETICO di `vipiMdFine` — senza, il riferimento resterebbe a schermo e non tornerebbe
    //      nel modello.
    // Il segno è un attributo sul campo e non un riferimento in una variabile: il campo può essere ridisegnato
    // da Blazor fra i due tempi, e l'attributo — che Blazor non gestisce — resta sull'elemento che sopravvive.
    //
    // 🔴 Il segno porta un GETTONE, uno per ogni apertura (revisione del 18 settembre 2026). Era un segno solo per
    // tutta la pagina: con due selettori aperti — blocco A e blocco B — la scelta fatta in A finiva nel campo di
    // B, e lo salvava. Ora `vipiSidPrendi` torna il gettone (vuoto = nessun campo) e `vipiSidInserisci` scrive
    // solo nel campo che lo porta. ⚠️ La posizione resta quella del clic sul tasto: chi torna nel campo e sposta
    // il cursore col selettore aperto, riceve il riferimento dove stava prima.
    var sidGettoni = 0;

    // Il segno «toccato»: al primo fuoco di un campo di testo, per sapere poi che il suo cursore è una scelta
    // di chi scrive. ⚠️ Sul DOCUMENTO e una volta sola: il segno di aggancio sta in una proprietà di `window` e
    // non in un `data-*`, che la navigazione enhanced cancella — e un secondo ascoltatore non farebbe danni, ma
    // è la regola già pagata (vedi memoria «segno agganciato in proprietà JS»).
    if (!window.__vipiSidToccato) {
        window.__vipiSidToccato = true;
        document.addEventListener('focusin', function (ev) {
            var t = ev.target;
            if (t && (t.tagName === 'TEXTAREA' || (t.tagName === 'INPUT' && t.type === 'text')))
                t.setAttribute('data-sid-toccato', '');
        }, true);
    }

    window.vipiSidPrendi = function (contenitore) {
        if (!contenitore) return '';
        var campo = function (x) { return x && (x.tagName === 'TEXTAREA' || (x.tagName === 'INPUT' && x.type === 'text')); };
        var el = campo(contenitore) ? contenitore : document.activeElement;
        if (!campo(el) || (el !== contenitore && !contenitore.contains(el))) return '';
        // Le intestazioni di una tabella non sono celle: il riferimento lì non si risolverebbe nelle anteprime.
        if (el !== contenitore && el.closest('thead')) return '';
        // Un campo MAI TOCCATO riceve in CODA, non in testa: il cursore «a zero» di un campo in cui nessuno ha
        // cliccato non è una scelta di chi scrive.
        //
        // 🔴 Ma «non ha il fuoco» non vuol dire «mai toccato», e fino al 21 settembre 2026 le due cose erano la
        // stessa regola. Segnalato dal campo: «la cite non la inserisce dove ho messo il cursore ma sempre alla
        // fine». Riprodotto: cursore a metà, poi un qualunque gesto che toglie il fuoco al campo prima del tasto
        // — un clic altrove, un tocco su schermo (dove il `preventDefault` sul mousedown non vale) — e il
        // riferimento finiva in coda, mentre il browser il cursore lo ricordava ancora, a metà.
        // Ora il campo che è stato toccato (`data-sid-toccato`, scritto al primo fuoco) usa il cursore che il
        // browser ricorda; solo quello mai toccato va in coda.
        var n = el.value.length;
        var ricorda = el === document.activeElement || el.hasAttribute('data-sid-toccato');
        var s = ricorda ? el.selectionStart : n;
        var e = ricorda ? el.selectionEnd : n;
        var gettone = 'g' + (++sidGettoni);
        el.setAttribute('data-sid-bersaglio', gettone + '|' + s + ',' + e);
        return gettone;
    };

    // Dal tasto sotto una tabella: il contenitore è il blocco marcato `data-sid-host`, e il campo è la cella
    // che ha il fuoco lì dentro. Vuoto = nessuna cella selezionata (il tasto lo dice).
    window.vipiSidPrendiDa = function (tasto) {
        return window.vipiSidPrendi(tasto && tasto.closest ? tasto.closest('[data-sid-host]') : null);
    };

    window.vipiSidInserisci = function (gettone, testo) {
        if (!gettone) return false;
        var el = document.querySelector('[data-sid-bersaglio^="' + gettone + '|"]');
        if (!el) return false;
        var p = el.getAttribute('data-sid-bersaglio').split('|')[1].split(',');
        el.removeAttribute('data-sid-bersaglio');
        var v = el.value, s = Math.min(+p[0] || 0, v.length), e = Math.min(+p[1] || s, v.length);
        // Uno spazio ai lati se manca: «Expect[[SID …]]then» si legge male anche risolto.
        var prima = s > 0 && !/\s/.test(v.charAt(s - 1)) ? ' ' : '';
        var dopo = e < v.length && !/[\s.,;:)]/.test(v.charAt(e)) ? ' ' : '';
        var dentro = prima + testo + dopo;
        el.value = v.substring(0, s) + dentro + v.substring(e);
        vipiMdFine(el, s + dentro.length, s + dentro.length);
        return true;
    };

    // ---- Elenchi annidati (16 settembre 2026) ----------------------------------------------------------
    //
    // Il livello si scrive coi TRATTINI: `- voce`, `-- voce`… per i puntati, `1) voce`, `-1) voce`… per i
    // numerati, fino a cinque livelli.
    //
    // 🔴 Le tre regex sono la COPIA JS di `VoceDiElenco.cs` (Vipi.Application), che è la regola del renderer e
    // del protettore della traduzione. Una terza copia non si evita — il gesto sta nel browser — ma si
    // presidia: `ElenchiNellEditorTests` confronta il TESTO di queste tre regex con quello delle tre C#, e
    // cade appena una delle due parti impara un marcatore che l'altra non conosce.
    var RX_VOCE_NUMERATA = /^([ \t]*(-*)(\d{1,3})[.)][ \t]+)(.*)$/;
    var RX_VOCE_TRATTINI = /^([ \t]*(-+)[ \t]+)(.*)$/;
    var RX_VOCE_SIMBOLO = /^([ \t]*[*+•][ \t]+)(.*)$/;
    var LIVELLI_ELENCO = 5;

    function vipiLivello(n) { return Math.max(1, Math.min(LIVELLI_ELENCO, n)); }

    // La riga è una voce? { livello, ordinata, numero, marcatore, testo } oppure null. Riga = marcatore + testo.
    function vipiVoce(riga) {
        var m = RX_VOCE_NUMERATA.exec(riga);
        if (m) return { livello: vipiLivello(m[2].length + 1), ordinata: true, numero: parseInt(m[3], 10), marcatore: m[1], testo: m[4] };
        m = RX_VOCE_TRATTINI.exec(riga);
        if (m) return { livello: vipiLivello(m[2].length), ordinata: false, numero: 1, marcatore: m[1], testo: m[3] };
        m = RX_VOCE_SIMBOLO.exec(riga);
        if (m) return { livello: 1, ordinata: false, numero: 1, marcatore: m[1], testo: m[2] };
        return null;
    }

    // Il marcatore canonico: un trattino per livello nei puntati, uno per livello OLTRE il primo nei numerati.
    // ⚠️ Il numerato si scrive `1)` e non `1.`: col trattino davanti, `-1.` si legge «meno uno punto».
    function vipiMarcatore(livello, ordinata, numero) {
        return ordinata ? '-'.repeat(livello - 1) + numero + ') ' : '-'.repeat(livello) + ' ';
    }

    function vipiRigaDi(testo, pos) { return testo.substring(0, pos).split('\n').length - 1; }

    function vipiInizioRiga(righe, k) {
        var o = 0;
        for (var j = 0; j < k; j++) o += righe[j].length + 1;
        return o;
    }

    // Rinumera i numerati dell'elenco contiguo [a, b]: ogni livello conta per conto suo, e una voce meno
    // profonda azzera i contatori di quelli sotto — è così che il renderer li apre e li chiude.
    // ⚠️ Il numero di PARTENZA di un elenco si rispetta sulle righe che il gesto non ha toccato: chi ha scritto
    // «3)» sta continuando un elenco interrotto da una tabella, e un Tab due righe più sotto non deve
    // riportarglielo a 1. Una riga appena spostata di livello, invece, riparte da 1: il suo vecchio numero
    // parlava di un altro elenco.
    function vipiRinumera(righe, a, b, toccate) {
        var conta = [], tipo = [];
        for (var i = a; i <= b; i++) {
            var x = vipiVoce(righe[i]);
            if (!x) continue;
            var L = x.livello;
            for (var k = L + 1; k <= LIVELLI_ELENCO; k++) { conta[k] = 0; tipo[k] = null; }
            if (!x.ordinata) { tipo[L] = 'u'; conta[L] = 0; continue; }
            if (tipo[L] !== 'o') conta[L] = (toccate[i] ? 1 : x.numero) - 1;
            tipo[L] = 'o';
            conta[L]++;
            righe[i] = x.marcatore.replace(/\d{1,3}/, String(conta[L])) + x.testo;
        }
    }

    // Applica `cambia(voce, riga)` alle righe TOCCATE dalla selezione (anche solo sfiorate: il cursore su una
    // riga basta), rinumera l'elenco in cui stanno e riscrive il campo. Torna true se qualcosa è cambiato.
    //
    // ⚠️ «Cambiato» si decide sulle righe toccate PRIMA di rinumerare. Il Tab si consuma solo se sposta
    // davvero una voce: a livello 5 (o Maiusc+Tab al primo) deve tornare a fare quel che fa sempre — cambiare
    // campo — e un elenco numerato male non deve «mangiarsi» il tasto per rimettere a posto i numeri.
    function vipiRiscriviRighe(el, cambia) {
        var v = el.value, s0 = el.selectionStart, e0 = el.selectionEnd;
        var e = (e0 > s0 && v.charAt(e0 - 1) === '\n') ? e0 - 1 : e0;   // selezione di righe intere
        var prima = v.split('\n');
        var righe = prima.slice();
        var p = vipiRigaDi(v, s0), u = vipiRigaDi(v, e);

        var toccate = {}, cambiato = false;
        for (var i = p; i <= u; i++) {
            righe[i] = cambia(vipiVoce(righe[i]), righe[i]);
            if (righe[i] !== prima[i]) { toccate[i] = true; cambiato = true; }
        }
        if (!cambiato) return false;

        var a = p; while (a > 0 && vipiVoce(righe[a - 1])) a--;
        var b = u; while (b < righe.length - 1 && vipiVoce(righe[b + 1])) b++;
        vipiRinumera(righe, a, b, toccate);

        el.value = righe.join('\n');
        if (s0 === e0) {
            // Cursore su una riga sola: resta nel TESTO, allo stesso punto — spostato solo di quanto è
            // cambiato il marcatore davanti.
            var vecchia = vipiVoce(prima[p]), nuova = vipiVoce(righe[p]);
            var mv = vecchia ? vecchia.marcatore.length : 0, mn = nuova ? nuova.marcatore.length : 0;
            var dentro = s0 - vipiInizioRiga(prima, p);
            var q = vipiInizioRiga(righe, p) + Math.max(mn, mn + dentro - mv);
            vipiMdFine(el, q, q);
        } else {
            vipiMdFine(el, vipiInizioRiga(righe, p), vipiInizioRiga(righe, u) + righe[u].length);
        }
        return true;
    }

    // Tasto «elenco puntato» / «elenco numerato». Marca le righe toccate col tipo scelto, TENENDO il livello
    // di quelle che già erano voci; se erano già tutte di quel tipo, le smarca (il tasto è un interruttore).
    window.vipiMdList = function (el, ordinato) {
        if (!el) return;
        var v = el.value, s0 = el.selectionStart, e0 = el.selectionEnd;
        var e = (e0 > s0 && v.charAt(e0 - 1) === '\n') ? e0 - 1 : e0;
        var righe = v.split('\n').slice(vipiRigaDi(v, s0), vipiRigaDi(v, e) + 1);
        var tutteMie = righe.every(function (r) {
            if (r.trim() === '') return true;
            var x = vipiVoce(r);
            return !!x && x.ordinata === ordinato;
        });
        vipiRiscriviRighe(el, function (x, r) {
            if (r.trim() === '') return r;
            if (tutteMie) return x ? x.testo : r;
            return vipiMarcatore(x ? x.livello : 1, ordinato, 1) + (x ? x.testo : r);
        });
    };

    // Rientra (delta +1) o riduce il rientro (delta -1) delle VOCI toccate. Le righe che non sono voci non si
    // toccano: un rientro su un capoverso non vuol dire niente, e inventargli un pallino sarebbe peggio.
    // Torna true se ha spostato qualcosa (serve al Tab per decidere se consumare il tasto).
    window.vipiMdRientro = function (el, delta) {
        if (!el) return false;
        return vipiRiscriviRighe(el, function (x, r) {
            if (!x) return r;
            var L = vipiLivello(x.livello + delta);
            return L === x.livello ? r : vipiMarcatore(L, x.ordinata, x.numero) + x.testo;
        });
    };

    // Scrive come se lo battesse chi scrive: `insertText` tiene l'annulla (Ctrl+Z) e marca il campo come
    // toccato, così il `change` all'uscita parte davvero. `setRangeText` è il ripiego dove non c'è, e lì il
    // `change` va mandato a mano — scrivere da JS non ne fa partire nessuno (vedi `vipiMdFine`).
    function vipiInserisci(el, testo) {
        var ok = false;
        try { ok = document.execCommand(testo === '' ? 'delete' : 'insertText', false, testo); } catch (err) { ok = false; }
        if (ok) return;
        el.setRangeText(testo, el.selectionStart, el.selectionEnd, 'end');
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Tab / Maiusc+Tab e Invio dentro un campo di prosa.
    //
    // ⚠️ Delegato sul documento come Ctrl+B, e per la stessa ragione: un `@onkeydown` di Blazor sarebbe un giro
    // di rete a ogni tasto battuto.
    //
    // ⚠️ Il TAB è anche il modo di girare la pagina con la sola tastiera. Qui si consuma SOLO se il cursore sta
    // su una voce e il livello cambia davvero: su un capoverso, al quinto livello, o con Maiusc+Tab al primo,
    // il tasto fa quel che fa ovunque. Nessuna casella in cui si entra e non si esce.
    if (!window.__vipiMdElenchi) {
        window.__vipiMdElenchi = true;
        document.addEventListener('keydown', function (e) {
            var el = e.target;
            if (!el || !el.matches || !el.matches('textarea.app-ta') || !el.closest('.rta')) return;
            if (e.ctrlKey || e.altKey || e.metaKey || e.isComposing) return;

            if (e.key === 'Tab') {
                if (window.vipiMdRientro(el, e.shiftKey ? -1 : 1)) e.preventDefault();
                return;
            }
            if (e.key !== 'Enter' || e.shiftKey || el.selectionStart !== el.selectionEnd) return;

            // Invio su una voce la continua: stessa voce, stesso livello, numero dopo.
            var v = el.value, pos = el.selectionStart;
            var ini = pos === 0 ? 0 : v.lastIndexOf('\n', pos - 1) + 1;
            var fin = v.indexOf('\n', pos);
            if (fin < 0) fin = v.length;
            var x = vipiVoce(v.substring(ini, fin));
            // Cursore DENTRO il marcatore (o prima): è un a capo normale, chi scrive sta spostando la voce giù.
            if (!x || pos - ini < x.marcatore.length) return;
            e.preventDefault();

            if (x.testo.trim() === '') {
                // Invio su una voce VUOTA: si esce di un livello, e dal primo si esce dall'elenco. È il gesto di
                // ogni editor di testo — il secondo Invio chiude quel che il primo aveva aperto.
                el.setSelectionRange(ini, fin);
                vipiInserisci(el, x.livello > 1 ? vipiMarcatore(x.livello - 1, x.ordinata, 1) : '');
                return;
            }
            vipiInserisci(el, '\n' + vipiMarcatore(x.livello, x.ordinata, x.numero + 1));
        });
    }

    // ---- Il campo che si adatta al testo (16 settembre 2026) --------------------------------------------
    //
    // Vale per le textarea marcate `data-adatta`: i campi di prosa (RichTextArea), le note delle aree di lavoro,
    // la descrizione di un incarico. NON per gli incolla-tabella, il convertitore o i poligoni: quelli sono
    // grandi apposta.
    //
    // Le regole, e il perché di ognuna:
    //   • cresce col testo fino al 60% dello schermo, poi scorre dentro: oltre, la barra di formattazione
    //     sopra il campo uscirebbe dalla vista mentre si scrive in fondo;
    //   • non scende sotto le righe che il campo dichiara (`rows`): misurando con `height:auto` il browser le
    //     rispetta da solo;
    //   • ⚠️ la maniglia resta: l'altezza trascinata a mano diventa il MINIMO. Senza, il primo tasto battuto
    //     rimangerebbe il gesto di chi ha allargato il campo per vederci meglio.
    //
    // ⚠️ Perché JS e non la sola `field-sizing: content`: Firefox non la conosce, e dove c'è smette di adattarsi
    // appena si trascina la maniglia. ⚠️ Perché la misura aspetta: un campo dentro una sezione CHIUSA non ha
    // altezza (`offsetParent` nullo), e misurato lì resterebbe a zero righe. Si misura quando compare — un
    // `toggle` che apre la sezione, o un nodo nuovo che Blazor mette in pagina.
    // Lo stato sta in PROPRIETÀ dell'elemento e non in `data-*`: la navigazione arricchita cancella gli
    // attributi che non ha scritto lei e tiene il nodo (vedi il T-070 in vipi-ui.js).
    var TETTO_ALTEZZA = 0.6;

    function vipiAdattabile(el) {
        return !!el && el.tagName === 'TEXTAREA' && el.hasAttribute('data-adatta');
    }

    window.vipiAdatta = function (el) {
        if (!vipiAdattabile(el) || !el.isConnected || el.offsetParent === null) return;
        // ⚠️ `height:auto` per misurare accorcia il campo per un istante, e se il campo sta sopra la parte di
        // pagina che si vede la pagina SALTA. Si rimette lo scorrimento dov'era.
        var sc = document.scrollingElement || document.documentElement;
        var y = sc.scrollTop;
        var cs = window.getComputedStyle(el);
        el.style.height = 'auto';
        var contenuto = cs.boxSizing === 'border-box'
            ? el.scrollHeight + parseFloat(cs.borderTopWidth) + parseFloat(cs.borderBottomWidth)
            : el.scrollHeight - parseFloat(cs.paddingTop) - parseFloat(cs.paddingBottom);
        var tetto = Math.round(window.innerHeight * TETTO_ALTEZZA);
        var h = Math.max(Math.min(contenuto, tetto), el.__vipiMinimo || 0);
        el.style.height = h + 'px';
        el.style.overflowY = contenuto > h + 1 ? 'auto' : 'hidden';
        el.__vipiAltezza = el.offsetHeight;
        el.__vipiAdattato = true;
        sc.scrollTop = y;
    };

    function vipiAdattaNuovi() {
        document.querySelectorAll('textarea[data-adatta]').forEach(function (el) {
            if (!el.__vipiAdattato) window.vipiAdatta(el);
        });
    }

    if (!window.__vipiAdattaWired) {
        window.__vipiAdattaWired = true;

        document.addEventListener('input', function (e) { if (vipiAdattabile(e.target)) window.vipiAdatta(e.target); });

        // La maniglia: se l'altezza al rilascio non è quella che le abbiamo dato noi, l'ha scelta chi scrive.
        document.addEventListener('pointerup', function (e) {
            var el = e.target;
            if (!vipiAdattabile(el) || !el.__vipiAltezza) return;
            if (Math.abs(el.offsetHeight - el.__vipiAltezza) <= 2) return;
            el.__vipiMinimo = el.offsetHeight;
            el.__vipiAltezza = el.offsetHeight;
            el.style.overflowY = 'auto';
        });

        // Una sezione che si apre: i campi dentro compaiono adesso. `toggle` non fa bubbling → cattura.
        document.addEventListener('toggle', function () { vipiAdattaNuovi(); }, true);

        // La larghezza cambia gli a capo, quindi l'altezza giusta. A riposo, non a ogni pixel.
        var attesaResize = null;
        window.addEventListener('resize', function () {
            clearTimeout(attesaResize);
            attesaResize = setTimeout(function () {
                document.querySelectorAll('textarea[data-adatta]').forEach(function (el) { el.__vipiAdattato = false; });
                vipiAdattaNuovi();
            }, 150);
        });

        // I campi che Blazor mette in pagina dopo: un editor che si apre, un blocco aggiunto, una navigazione.
        var attesaNodi = null;
        new MutationObserver(function () {
            if (attesaNodi) return;
            attesaNodi = setTimeout(function () { attesaNodi = null; vipiAdattaNuovi(); }, 100);
        }).observe(document.documentElement, { childList: true, subtree: true });

        vipiAdattaNuovi();
    }

    // Ctrl/Cmd+B/I/U dentro una textarea markdown. ⚠️ Delegato sul documento e NON legato al componente:
    // un `@onkeydown` di Blazor sarebbe un giro di rete a OGNI tasto battuto, su ogni campo dell'editor.
    // Qui il costo è zero finché non si preme la combinazione.
    if (!window.__vipiMdKeys) {
        window.__vipiMdKeys = true;
        document.addEventListener('keydown', function (e) {
            if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
            var el = e.target;
            // ⚠️ Si riconosce dall'INVOLUCRO (`.rta`), non da una classe propria sulla textarea: una classe
            // messa lì solo per farsi trovare dal JS sarebbe un campo con un vestito che nessun foglio
            // cuce — e c'è una prova che lo vieta, perché è così che un campo finisce coi colori del browser.
            if (!el || !el.matches || !el.matches('textarea.app-ta') || !el.closest('.rta')) return;
            var k = (e.key || '').toLowerCase();
            if (k === 'b') { e.preventDefault(); window.vipiMdWrap(el, '**', '**'); }
            else if (k === 'i') { e.preventDefault(); window.vipiMdWrap(el, '*', '*'); }
            else if (k === 'u') { e.preventDefault(); window.vipiMdWrap(el, '__', '__'); }
        });
    }

    // ---- Suggerimento scelto da un <datalist>: vale SUBITO, non all'uscita dal campo --------------------
    //
    // ⚠️ Segnalato dal campo il 14 settembre 2026: scrivendo a mano il punto di una SID «devo cliccare più
    // volte sul suggerimento perché vada». I campi con `list=` sono legati con `@bind`, cioè sul `change`.
    // Quando si sceglie una voce dall'elenco il browser scrive il valore e manda un `input` di tipo
    // `insertReplacementText`; il `change`, se il campo ha ancora il fuoco, può arrivare solo all'uscita. Fino
    // ad allora il server non sa niente: il campo resta segnato «punto sconosciuto», niente si salva, e chi
    // guarda riprova col suggerimento — finché un clic altrove fa uscire dal campo e il valore «va».
    // Qui la scelta dall'elenco diventa un `change` subito. Solo per quel tipo di `input` e solo se il valore
    // è davvero una voce dell'elenco: chi scrive a mano continua a salvare all'uscita, come prima.
    // 🔴 E il `change` del browser che RIPETE quello sintetico non passa (17 settembre 2026). La riga qui sopra
    // diceva «chi riceve ignori il valore invariato», e non bastava: i due `change` arrivano uno dietro l'altro,
    // la prima scrittura è ancora in volo quando parte la seconda, e sullo stesso DbContext del circuito è
    // «A second operation was started» — la pagina muore. Visto in produzione scegliendo «ILS» nella colonna
    // Tipo delle radioassistenze (vSOP militare e anagrafica). Il segno `__vipiAtteso` dice «ho già mandato
    // questo valore»: il primo `change` vero con quel valore si mangia, qualunque altra cosa lo cancella.
    if (!window.__vipiDatalistPick) {
        window.__vipiDatalistPick = true;
        document.addEventListener('input', function (e) {
            var el = e.target;
            if (!el || el.tagName !== 'INPUT' || !el.list) return;
            if (e.inputType && e.inputType !== 'insertReplacementText') { el.__vipiAtteso = undefined; return; }
            var v = el.value;
            var voci = el.list.options;
            for (var i = 0; i < voci.length; i++) {
                if (voci[i].value === v) {
                    el.__vipiAtteso = v;
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    return;
                }
            }
        }, true);
        // Su `window` e in cattura: arriva prima dell'ascoltatore di Blazor, che sta sul documento.
        window.addEventListener('change', function (e) {
            var el = e.target;
            if (!el || el.__vipiAtteso === undefined || !e.isTrusted) return;
            var atteso = el.__vipiAtteso;
            el.__vipiAtteso = undefined;
            if (el.value === atteso) e.stopImmediatePropagation();
        }, true);
    }

    // ---- Larghezza delle colonne col mouse (S4, 23 settembre 2026) ------------------------------------
    //
    // La maniglia `.col-grip` sta sul bordo destro di ogni intestazione nell'editor di una tabella generica.
    // Trascinandola la colonna si allarga dal vivo (il suo `<col>` del colgroup, che nell'editor c'è sempre) e il
    // campo «%» della colonna segue; al rilascio si scrive quel campo e gli si manda un `change`. ⚠️ Nessuna
    // chiamata nuova verso .NET: il salvataggio è quello del campo, cioè la STESSA strada di chi scrive il numero
    // a mano — un solo posto dove la larghezza si valida e si salva. Doppio clic = torna automatica.
    // Il campo non ha il fuoco, quindi il browser non manda un suo `change`: niente doppio salvataggio.
    // Delegato sul documento e installato UNA volta: la tabella la ridisegna Blazor, e questo file può essere
    // rieseguito da una navigazione arricchita.
    if (!window.__vipiColGrip) {
        window.__vipiColGrip = true;
        var colDi = function (grip) {
            var th = grip.closest('th');
            var table = th && th.closest('table');
            if (!table) return null;
            var i = Array.prototype.indexOf.call(th.parentElement.children, th);
            var col = table.querySelectorAll(':scope > colgroup > col')[i];
            var campo = th.querySelector('input[type=number]');
            return col && campo ? { th: th, table: table, col: col, campo: campo } : null;
        };
        var salva = function (campo, valore) {
            campo.value = valore;
            campo.dispatchEvent(new Event('change', { bubbles: true }));
        };
        document.addEventListener('pointerdown', function (ev) {
            var grip = ev.target && ev.target.closest ? ev.target.closest('.col-grip') : null;
            if (!grip || ev.button !== 0) return;
            var c = colDi(grip);
            if (!c) return;
            ev.preventDefault();
            var larga = c.table.getBoundingClientRect().width;
            var x0 = ev.clientX, w0 = c.th.getBoundingClientRect().width;
            var stile = c.col.getAttribute('style'), prima = c.campo.value, pct = null;
            try { grip.setPointerCapture(ev.pointerId); } catch (e) { }
            c.table.classList.add('col-trascina');
            var muovi = function (e) {
                pct = Math.max(1, Math.min(100, Math.round((w0 + e.clientX - x0) / larga * 100)));
                c.col.style.width = pct + '%';
                c.campo.value = pct;
            };
            var fine = function (e) {
                grip.removeEventListener('pointermove', muovi);
                grip.removeEventListener('pointerup', fine);
                grip.removeEventListener('pointercancel', fine);
                c.table.classList.remove('col-trascina');
                if (e.type === 'pointerup' && pct !== null && String(pct) !== prima) {
                    salva(c.campo, pct);      // il ridisegno di Blazor riscrive lo stile del <col>
                    return;
                }
                // Niente di cambiato, o gesto annullato: si rimette com'era.
                if (stile === null) c.col.removeAttribute('style'); else c.col.setAttribute('style', stile);
                c.campo.value = prima;
            };
            grip.addEventListener('pointermove', muovi);
            grip.addEventListener('pointerup', fine);
            grip.addEventListener('pointercancel', fine);
        });
        document.addEventListener('dblclick', function (ev) {
            var grip = ev.target && ev.target.closest ? ev.target.closest('.col-grip') : null;
            var c = grip && colDi(grip);
            if (c && c.campo.value !== '') salva(c.campo, '');
        });
    }

    // Scroll a un'ancora lasciando spazio per la barra sticky (altezza misurata a runtime).
    window.vipiScrollTo = function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        var bar = document.querySelector('.editor-bar');
        var off = (bar ? bar.offsetHeight : 0) + 12;
        var y = el.getBoundingClientRect().top + window.pageYOffset - off;
        window.scrollTo({ top: y, behavior: vipiScorrimento() });
    };
})();
