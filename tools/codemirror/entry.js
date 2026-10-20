// Il pacchetto di CodeMirror 6 per la pagina Testo del Sector Lab (riordino, D12): quel che usa
// sectorlab-testo.js, esposto come window.CM. Niente rete: il file sta nell'app.
export { EditorState, StateField, StateEffect, Compartment, Prec, RangeSetBuilder, Transaction } from "@codemirror/state";
export {
    EditorView, keymap, lineNumbers, highlightActiveLine, highlightActiveLineGutter, drawSelection,
    Decoration, ViewPlugin, gutter, GutterMarker, placeholder,
} from "@codemirror/view";
export { history, historyKeymap, defaultKeymap, indentWithTab } from "@codemirror/commands";
export { search, searchKeymap, highlightSelectionMatches, openSearchPanel } from "@codemirror/search";
export { StreamLanguage, syntaxHighlighting, HighlightStyle } from "@codemirror/language";
export { tags } from "@lezer/highlight";
export { linter, lintGutter, setDiagnostics } from "@codemirror/lint";
export { autocompletion, completionKeymap } from "@codemirror/autocomplete";
