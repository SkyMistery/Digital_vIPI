// Simulazione di vipi-boot.js: il primo caricamento di vipi-awos.js fallisce, poi la rete torna.
// U-209 (revisione 3, S38). Uso: node docs/history/revisione-totale-3/sim-boot.js src/Vipi.Ui/wwwroot/vipi-boot.js
// Sul codice di prima stampa ROSSO (una richiesta sola); con falliscono = 99 il tetto ferma i tentativi a 3.
const fs = require('fs');
const vm = require('vm');
const codice = fs.readFileSync(process.argv[2], 'utf8');

const richieste = [];
let falliscono = 1;          // quante richieste di awos falliscono prima che la rete torni
const listeners = {};
const tag = { getAttribute: n => n === 'data-awos-src' ? '/vipi-awos.js' : null };
const head = {
    appendChild(el) {
        el.parentNode = head;
        richieste.push(el.src);
        setTimeout(() => {
            if (falliscono > 0) { falliscono--; el.onerror && el.onerror(); }
            else el.onload && el.onload();
        }, 10);
    },
    removeChild() {},
};
const document = {
    currentScript: tag,
    head, body: {},
    createElement: () => ({ setAttribute() {} }),
    querySelector: sel => sel === '.awos' ? {} : null,
};
class MutationObserver { constructor(cb) { this.cb = cb; } observe() {} disconnect() {} }
const ctx = {
    document, MutationObserver, setTimeout, console: { warn() {} },
    Blazor: { addEventListener: (n, f) => { listeners[n] = f; } },
};
ctx.window = ctx;
vm.createContext(ctx);
vm.runInContext(codice, ctx);

// Dopo il guasto l'utente cambia scalo: navigazione enhanced.
setTimeout(() => listeners.enhancedload(), 100);
setTimeout(() => {
    console.log('richieste di vipi-awos.js:', richieste.length);
    console.log(richieste.length >= 2 ? 'VERDE: ritentato' : 'ROSSO: mai più richiesto');
    process.exit(0);
}, 3000);
