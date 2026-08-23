# -*- coding: utf-8 -*-
"""Classi CSS che nessuna sorgente nomina.

Metodo prudente: si cerca il NOME NUDO in TUTTO il testo di ogni .razor/.cs/.js (non solo dentro
class="..."), cosi' una classe composta a pezzi (`"tag-" + x`, `@(on ? "on" : "")`) non risulta morta per
sbaglio. Le classi con nome molto generico vanno comunque guardate a mano."""
import io, os, re, sys

root = sys.argv[1]
css = os.path.join(root, 'src', 'Vipi.Ui', 'wwwroot', 'vipi-theme.css')

testo = []
for base, dirs, files in os.walk(os.path.join(root, 'src')):
    dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
    for f in files:
        # i .resx CONTANO: due stringhe di risorsa portano HTML con classi (guida-kbd, rwy-key)
        #    e finiscono a schermo via MarkupString. Senza, risultano morte e non lo sono.
        if f.endswith(('.razor', '.cs', '.js', '.html', '.cshtml', '.resx')):
            try:
                testo.append(io.open(os.path.join(base, f), encoding='utf-8-sig', errors='ignore').read())
            except Exception:
                pass
tutto = '\n'.join(testo)
# confronto MINUSCOLO: `.node-badge.fss` nasce da un `"FSS"` piu' `.ToLowerInvariant()`, e il nome
# in minuscolo non compare da nessuna parte. Costa qualche falso negativo, evita un falso positivo
# che a schermo si vede.
tutto = tutto.lower()

s = io.open(css, encoding='utf-8-sig').read()
s_nc = re.sub(r'/\*.*?\*/', ' ', s, flags=re.S)          # via i commenti
selettori = re.findall(r'([^{}]+)\{[^{}]*\}', s_nc)      # restano i selettori
classi = {}
for sel in selettori:
    if '@' in sel:
        continue
    for c in re.findall(r'\.(-?[_a-zA-Z][\w-]*)', sel):
        classi.setdefault(c, set()).add(' '.join(sel.split())[:90])

# un solo passaggio sul testo: l'insieme dei token [A-Za-z0-9_-] delle sorgenti
token = set(re.findall(r'[a-z_][\w-]*', tutto))
morte = [c for c in sorted(classi) if c.lower() not in token]

print('classi nel foglio: %d - senza alcuna citazione nelle sorgenti: %d\n' % (len(classi), len(morte)))
for c in morte:
    print('  .%-22s  %s' % (c, ' | '.join(sorted(classi[c])[:2])))
