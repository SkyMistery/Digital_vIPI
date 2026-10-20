# Il pacchetto di CodeMirror per la pagina Testo

La pagina **Testo** del Sector Lab (riordino, D12) usa CodeMirror 6 **dentro l'app, senza rete**: un file solo,
`src/Vipi.SectorLab.Ui/wwwroot/vendor/codemirror/codemirror.js`, come Leaflet. Si fa una volta e si tiene nel repo; si
rifà solo per cambiare versione o per esporre un pezzo nuovo (in `entry.js`).

Le versioni sono **esatte** in `package.json`: le ultime uscite da almeno due settimane quando il pacchetto è stato fatto
(20 ottobre 2026). Per rifarlo, fuori dal repo (la cartella `node_modules` non si committa):

```bash
cp tools/codemirror/package.json tools/codemirror/entry.js <cartella di lavoro>/
cd <cartella di lavoro> && npm install && npm run build
cp codemirror.js <repo>/src/Vipi.SectorLab.Ui/wwwroot/vendor/codemirror/codemirror.js
```

Licenza: CodeMirror e Lezer sono MIT (Marijn Haverbeke e gli altri autori); il testo delle licenze sta in fondo al file
(`--legal-comments=eof`) e in `vendor/codemirror/LICENSE`.
