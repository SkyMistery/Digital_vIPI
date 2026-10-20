// La pagina Testo del Sector Lab (riordino, D12, D13): l'editor di un file del sector, CodeMirror 6 dal pacchetto in
// vendor/codemirror (window.CM, niente rete). Il testo lo dà il server; «Applica» glielo rimanda e diventa una modifica
// in sospeso come le altre. Le righe dei metadati (//@, //@@) sono bloccate finché l'AOD non le sblocca: una modifica
// che le cambia, le toglie o ne aggiunge una non passa.
(() => {
    "use strict";
    const editori = new Map();   // id dell'elemento → { view, bloccate, dotnet, sporco }

    const eUnTag = testo => /^\s*\/\/@/.test(testo);

    // Le righe dei tag fra due posizioni del documento, in ordine.
    function tagFra(doc, da, a) {
        const tag = [];
        for (let n = doc.lineAt(da).number, ultima = doc.lineAt(a).number; n <= ultima; n++) {
            const riga = doc.line(n).text;
            if (eUnTag(riga)) tag.push(riga);
        }
        return tag;
    }

    // D13: con i metadati bloccati una modifica che tocca le righe //@ (le cambia, le toglie, ne aggiunge) si scarta.
    function filtroDeiTag(id) {
        return CM.EditorState.transactionFilter.of(tr => {
            const e = editori.get(id);
            if (!tr.docChanged || !e || !e.bloccate) return tr;
            let tocca = false;
            tr.changes.iterChangedRanges((daA, aA, daB, aB) => {
                if (tocca) return;
                const prima = tagFra(tr.startState.doc, daA, aA), dopo = tagFra(tr.newDoc, daB, aB);
                tocca = prima.length !== dopo.length || prima.some((t, i) => t !== dopo[i]);
            });
            if (!tocca) return tr;
            e.dotnet?.invokeMethodAsync("TagBloccati");
            return [];
        });
    }

    // Le righe dei tag in grigio, col lucchetto se bloccate.
    function segnoDeiTag(id) {
        const riga = CM.Decoration.line({ class: "cm-lab-tag" });
        const costruisci = view => {
            const b = new CM.RangeSetBuilder();
            for (const { from, to } of view.visibleRanges)
                for (let pos = from; pos <= to;) {
                    const l = view.state.doc.lineAt(pos);
                    if (eUnTag(l.text)) b.add(l.from, l.from, riga);
                    pos = l.to + 1;
                }
            return b.finish();
        };
        return CM.ViewPlugin.fromClass(class {
            constructor(view) { this.decorations = costruisci(view); }
            update(u) { if (u.docChanged || u.viewportChanged) this.decorations = costruisci(u.view); }
        }, { decorations: v => v.decorations });
    }

    // I colori per genere di campo, uguali per tutti i file del sector (righe a punto e virgola): il primo campo (il nome),
    // le coordinate e i numeri, i separatori, i commenti, i metadati, le intestazioni di sezione.
    const linguaggio = CM.StreamLanguage.define({
        startState: () => ({ campo: 0 }),
        token(stream, s) {
            if (stream.sol()) {
                s.campo = 0;
                if (stream.match(/^\s*\/\/@/)) { stream.skipToEnd(); return "meta"; }
                if (stream.match(/^\s*\/\//)) { stream.skipToEnd(); return "comment"; }
                if (stream.match(/^\s*\[[^\]]*\]\s*$/)) return "heading";
            }
            if (stream.eat(";")) { s.campo++; return "punctuation"; }
            if (stream.match(/^[NSEW]\d{2,3}\.\d{2}\.\d{2}\.\d{1,3}(?=;|$|\s)/)) return "number";
            if (stream.match(/^-?\d+(\.\d+)?(?=;|$|\s)/)) return "number";
            if (stream.match(/^\s*\/\/.*/)) return "comment";   // un commento in coda (vietato in Aurora, ma si vede)
            stream.match(/^[^;]+/) || stream.next();
            return s.campo === 0 ? "variableName" : "string";
        },
    });

    const colori = CM.HighlightStyle.define([
        { tag: CM.tags.meta, color: "var(--lab-tenue)", fontStyle: "italic" },
        { tag: CM.tags.comment, color: "var(--lab-ok)" },
        { tag: CM.tags.heading, color: "var(--lab-titoli)", fontWeight: "bold" },
        { tag: CM.tags.punctuation, color: "var(--lab-tenue)" },
        { tag: CM.tags.number, color: "var(--lab-strato-aip)" },
        { tag: CM.tags.variableName, color: "var(--lab-accento)", fontWeight: "600" },
        { tag: CM.tags.string, color: "var(--lab-testo)" },
    ]);

    const tema = CM.EditorView.theme({
        "&": { height: "100%", backgroundColor: "var(--lab-superficie)", color: "var(--lab-testo)", fontSize: "13px" },
        ".cm-scroller": { fontFamily: "'IBM Plex Mono', ui-monospace, monospace", lineHeight: "1.45" },
        ".cm-gutters": { backgroundColor: "var(--lab-fondo)", color: "var(--lab-tenue)", borderRight: "1px solid var(--lab-linea)" },
        ".cm-activeLine": { backgroundColor: "var(--lab-sopra)" },
        ".cm-activeLineGutter": { backgroundColor: "var(--lab-scelto)" },
        "&.cm-focused .cm-cursor": { borderLeftColor: "var(--lab-testo)" },
        ".cm-selectionBackground, &.cm-focused .cm-selectionBackground": { backgroundColor: "var(--lab-scelto) !important" },
        ".cm-lab-tag": { backgroundColor: "var(--lab-fondo)" },
    });

    function apri(id, testo, opzioni) {
        const elemento = document.getElementById(id);
        if (!elemento) return false;
        chiudi(id);
        const e = { view: null, bloccate: true, dotnet: opzioni.dotnet ?? null, sporco: false, sola: !opzioni.modificabile };
        editori.set(id, e);
        e.view = new CM.EditorView({
            parent: elemento,
            state: CM.EditorState.create({
                doc: testo,
                extensions: [
                    CM.lineNumbers(), CM.highlightActiveLineGutter(), CM.highlightActiveLine(), CM.drawSelection(),
                    CM.history(), CM.search({ top: true }), CM.highlightSelectionMatches(),
                    CM.keymap.of([...CM.defaultKeymap, ...CM.historyKeymap, ...CM.searchKeymap]),
                    linguaggio, CM.syntaxHighlighting(colori), tema, segnoDeiTag(id), filtroDeiTag(id),
                    CM.EditorState.readOnly.of(!opzioni.modificabile),
                    CM.EditorView.updateListener.of(u => {
                        if (u.docChanged && !e.sporco && !e.ricaricando) {
                            e.sporco = true;
                            e.dotnet?.invokeMethodAsync("Cambiato", true);
                        }
                    }),
                ],
            }),
        });
        elemento.dataset.righe = String(e.view.state.doc.lines);
        return true;
    }

    function chiudi(id) {
        const e = editori.get(id);
        if (e) { e.view.destroy(); editori.delete(id); }
    }

    // Il testo di adesso, per «Applica».
    const testo = id => editori.get(id)?.view.state.doc.toString() ?? null;

    // Dopo «Applica» (o «Scarta») il testo nuovo è quello di riferimento.
    function ricarica(id, nuovo) {
        const e = editori.get(id);
        if (!e) return;
        const v = e.view;
        // La ricarica non è una modifica dell'AOD: passa anche sui tag, e il testo non diventa «da applicare».
        e.bloccate = false;
        e.ricaricando = true;
        v.dispatch({ changes: { from: 0, to: v.state.doc.length, insert: nuovo } });
        e.ricaricando = false;
        e.bloccate = e.bloccateScelte ?? true;
        e.sporco = false;
        v.dom.parentElement.dataset.righe = String(v.state.doc.lines);
    }

    function sblocca(id, sbloccate) {
        const e = editori.get(id);
        if (!e) return;
        e.bloccate = !sbloccate;
        e.bloccateScelte = e.bloccate;
        e.view.dom.classList.toggle("cm-lab-tag-sbloccati", !!sbloccate);
    }

    function vaiAllaRiga(id, numero) {
        const v = editori.get(id)?.view;
        if (!v) return;
        const l = v.state.doc.line(Math.max(1, Math.min(numero, v.state.doc.lines)));
        v.dispatch({ selection: { anchor: l.from }, effects: CM.EditorView.scrollIntoView(l.from, { y: "center" }) });
        v.focus();
    }

    window.sectorlabTesto = { apri, chiudi, testo, ricarica, sblocca, vaiAllaRiga };
})();
