"""Legge il registro del giorno scaricato dal server (§A59) e CONTA: dove va il tempo, pagina per pagina.

Uso (dalla radice del repo):
    python tools/registro-del-giorno.py                          # cartella predefinita: ../diagnostica
    python tools/registro-del-giorno.py <cartella>
    python tools/registro-del-giorno.py --dal 2026-09-18 --versione 1.31.0

Legge due famiglie di file, un file per giorno UTC, tenuti sette giorni dal sito:
  - richieste-AAAA-MM-GG.tsv  una riga per richiesta (RegistroRichieste): rotta, esito, ms, autenticato;
  - log-AAAA-MM-GG.txt        le righe Vipi.* da Information in su (RegistroInformativo).

Esiste perche' un file di migliaia di righe non si sfoglia, si conta (memoria «diagnostica-di-produzione», regola 4).

⚠️ Il tempo si confronta PER ROTTA e PER VERSIONE, non in totale: una pagina nuova e pesante sposta la media di tutto.
⚠️ Le richieste di notte sono poche: la tabella per ora serve a non confrontare la notte col giorno (regola 6).
⚠️ Per GET /_blazor (esito 101, il circuito) e GET /vsop/live/atc (stream SSE) i millisecondi sono la VITA della
   connessione, non un tempo di risposta: stanno a parte. Il 17-set un /vsop/live/atc da 50 s sembrava una pagina lenta.
⚠️ I file stanno FUORI dal repo e non si committano.
"""
import argparse
import collections
import glob
import io
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")  # la console di Windows e' cp1252

ap = argparse.ArgumentParser()
ap.add_argument("cartella", nargs="?", default=os.path.join(os.path.dirname(__file__), "..", "..", "diagnostica"))
ap.add_argument("--dal", help="primo giorno da leggere, AAAA-MM-GG")
ap.add_argument("--versione", help="solo le righe la cui versione contiene questo testo")
ap.add_argument("--lente", type=int, default=15, help="quante richieste lente mostrare una per una")
arg = ap.parse_args()


def giorni(prefisso, estensione):
    for file in sorted(glob.glob(os.path.join(arg.cartella, f"{prefisso}-*.{estensione}"))):
        m = re.search(r"-(\d{4}-\d{2}-\d{2})\." + estensione + "$", file)
        if m and (not arg.dal or m.group(1) >= arg.dal):
            yield m.group(1), file


def percentile(valori, p):
    if not valori:
        return 0
    v = sorted(valori)
    return v[min(len(v) - 1, int(round(p / 100 * (len(v) - 1))))]


def tabella(intestazione, righe):
    righe = [tuple(str(c) for c in r) for r in righe]
    larghezze = [max(len(str(x)) for x in col) for col in zip(intestazione, *righe)]
    fmt = "  ".join("{:<%d}" % larghezze[0] if i == 0 else "{:>%d}" % w for i, w in enumerate(larghezze))
    print("  " + fmt.format(*intestazione))
    for r in righe:
        print("  " + fmt.format(*r))
    print()


# ---------------------------------------------------------------- richieste
Richiesta = collections.namedtuple("Richiesta", "giorno ora pid versione metodo rotta percorso esito ms autenticato")
richieste = []
troncati = []
for giorno, file in giorni("richieste", "tsv"):
    colonne = None
    for riga in open(file, encoding="utf-8-sig"):
        riga = riga.rstrip("\n")
        if riga.startswith("# troncato:"):
            troncati.append(f"{os.path.basename(file)}: {riga}")
        if not riga or riga.startswith("#"):
            continue
        if colonne is None:
            colonne = riga.split("\t")
            continue
        c = dict(zip(colonne, riga.split("\t")))
        try:
            r = Richiesta(giorno, c["ora"], c["pid"], c["versione"], c["metodo"], c["rotta"], c["percorso"],
                          int(c["esito"]), int(c["ms"]), c["autenticato"] == "1")
        except (KeyError, ValueError):
            continue  # una riga pestata da due processi: si salta
        if arg.versione and arg.versione not in r.versione:
            continue
        richieste.append(r)

for t in troncati:
    print(f"🔴 {t}")
if troncati:
    print()

# Le connessioni che durano quanto la pagina: il circuito Blazor e lo stream SSE della vista live.
LUNGHE = {"/vsop/live/atc"}
lunga = lambda r: r.esito == 101 or r.rotta in LUNGHE
circuiti = [r for r in richieste if lunga(r)]
pagine = [r for r in richieste if not lunga(r)]

if not richieste:
    print(f"Nessun richieste-*.tsv utile in {os.path.abspath(arg.cartella)}.\n")
else:
    print("Per giorno")
    per_giorno = collections.defaultdict(list)
    for r in richieste:
        per_giorno[r.giorno].append(r)
    tabella(("giorno", "richieste", "autenticate", "4xx", "5xx", "lunghe", "processi"),
            [(g, sum(1 for r in rr if not lunga(r)), sum(1 for r in rr if r.autenticato and not lunga(r)),
              sum(1 for r in rr if 400 <= r.esito < 500), sum(1 for r in rr if r.esito >= 500),
              sum(1 for r in rr if lunga(r)), len({r.pid for r in rr}))
             for g, rr in sorted(per_giorno.items())])

    print("Dove va il tempo: per rotta e versione, ordinato per tempo totale (ms)")
    gruppi = collections.defaultdict(list)
    for r in pagine:
        gruppi[(r.metodo + " " + r.rotta, r.versione)].append(r)
    righe = []
    for (rotta, versione), rr in gruppi.items():
        ms = [r.ms for r in rr]
        righe.append((rotta[:60], versione, len(ms), percentile(ms, 50), percentile(ms, 95), max(ms), sum(ms),
                      sum(1 for r in rr if r.esito >= 500)))
    righe.sort(key=lambda x: -x[6])
    tabella(("rotta", "versione", "n", "p50", "p95", "max", "totale", "5xx"), righe[:30])
    if len(righe) > 30:
        print(f"  … altre {len(righe) - 30} rotte\n")

    if circuiti:
        print("Connessioni lunghe (circuito /_blazor 101, stream /vsop/live/atc): la vita, in secondi")
        per_v = collections.defaultdict(list)
        for r in circuiti:
            per_v[(r.rotta, r.versione)].append(r)
        tabella(("connessione", "versione", "n", "mediana", "p95", "max", "autenticati"),
                [(k, v, len(rr), round(percentile([r.ms / 1000 for r in rr], 50)),
                  round(percentile([r.ms / 1000 for r in rr], 95)), round(max(r.ms for r in rr) / 1000),
                  sum(1 for r in rr if r.autenticato)) for (k, v), rr in sorted(per_v.items())])

    print(f"Le {arg.lente} richieste più lente")
    tabella(("giorno", "ora fine", "ms", "esito", "aut", "percorso"),
            [(r.giorno, r.ora, r.ms, r.esito, "1" if r.autenticato else "0", r.metodo + " " + r.percorso[:70])
             for r in sorted(pagine, key=lambda r: -r.ms)[:arg.lente]])

    print("Per ora UTC (tutti i giorni insieme)")
    per_ora = collections.defaultdict(list)
    for r in pagine:
        per_ora[r.ora[:2]].append(r.ms)
    tabella(("ora", "richieste", "p50", "p95"),
            [(h, len(v), percentile(v, 50), percentile(v, 95)) for h, v in sorted(per_ora.items())])

# ---------------------------------------------------------------- log
voci = []
for giorno, file in giorni("log", "txt"):
    for riga in open(file, encoding="utf-8-sig"):
        m = re.match(r"(\d\d:\d\d:\d\d\.\d{3}) (\d+) (\w{3}) (\S+) · (.*)", riga.rstrip("\n"))
        if m:
            voci.append((giorno, m.group(3), m.group(4), m.group(5)))

if not voci:
    print(f"Nessun log-*.txt utile in {os.path.abspath(arg.cartella)}.")
else:
    print("Righe di log per categoria e livello")
    conta = collections.Counter((c, l) for _, l, c, _ in voci)
    tabella(("categoria", "livello", "righe"), [(c, l, n) for (c, l), n in conta.most_common(25)])

    print("I messaggi più frequenti (numeri e codici normalizzati)")
    normalizza = lambda s: re.sub(r"\b(?!IVAO|NOAA|UTC)[A-Z]{4}\b", "ICAO", re.sub(r"\d+([.,]\d+)?", "#", s))[:110]
    conta = collections.Counter((l, c, normalizza(t)) for _, l, c, t in voci)
    tabella(("n", "liv", "categoria", "messaggio"), [(n, l, c, t) for (l, c, t), n in conta.most_common(20)])
