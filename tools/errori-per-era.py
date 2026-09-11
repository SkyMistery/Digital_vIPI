"""Legge i file di diagnostica scaricati dal server e CONTA le voci di errori-richieste.txt PER ERA.

Uso (dalla radice del repo):
    python tools/errori-per-era.py                 # cartella predefinita: ../diagnostica
    python tools/errori-per-era.py <cartella>

Esiste perche' le regole di lettura sono state pagate una alla volta (memoria «diagnostica-di-produzione»)
e lo script che le applica veniva riscritto a mano a ogni giro:

  - il file NON si azzera fra una consegna e l'altra: si conta per ERA, cioe' per versione in servizio
    all'ora della voce, e i confini li da' avvii.txt (la prima riga AVVIO di ogni versione);
  - si CONTA, non si sfoglia: famiglia = (tipo d'eccezione, primo fotogramma Vipi., inizio del messaggio);
  - un'era muta non assolve niente se era notte: accanto ai conteggi si stampano le richieste dell'era e
    quante accensioni ha svegliato il ping di salute.

⚠️ avvii.txt si accorcia da solo (lo dice nella prima riga): le voci piu' vecchie della sua prima riga
finiscono nell'era «(prima di avvii.txt)», che non si sa attribuire.
⚠️ I file di diagnostica portano i VID degli utenti: stanno FUORI dal repo e non si committano.
"""
import collections
import io
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")  # la console di Windows e' cp1252

cartella = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "diagnostica")
leggi = lambda nome: open(os.path.join(cartella, nome), encoding="utf-8-sig").read()

avvii = leggi("avvii.txt").splitlines()
inizi = []  # (orario, versione) di ogni AVVIO, in ordine
for riga in avvii:
    m = re.match(r"(\S+ \S+)Z\s+AVVIO\s+(\S+ · \S+)", riga)
    if m:
        inizi.append((m.group(1), m.group(2)))


def era(orario):
    versione = "(prima di avvii.txt)"
    for inizio, v in inizi:
        if inizio <= orario:
            versione = v
        else:
            break
    return versione


print("Prima comparsa di ogni versione in avvii.txt:")
viste = set()
for inizio, v in inizi:
    if v not in viste:
        viste.add(v)
        print(f"   {inizio}Z  {v}")

# Richieste e risvegli per era, dalle righe ARRESTO (si attribuiscono all'avvio che le precede).
richieste = collections.Counter()
dal_ping = collections.Counter()
accensioni = collections.Counter()
versione_corrente = None
for riga in avvii:
    m = re.match(r"(\S+ \S+)Z\s+AVVIO\s+(\S+ · \S+)", riga)
    if m:
        versione_corrente = m.group(2)
        continue
    m = re.search(r"ARRESTO.*richieste (\d+).*svegliato da (\S+)", riga)
    if m and versione_corrente:
        accensioni[versione_corrente] += 1
        richieste[versione_corrente] += int(m.group(1))
        if "health" in m.group(2):
            dal_ping[versione_corrente] += 1

testo = leggi("errori-richieste.txt")
voci = []
for blocco in re.split(r"\n-{70,}\n", testo):
    m = re.match(r"(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d) UTC · codice (\S+)\n(.*)", blocco, re.S)
    if not m:
        continue
    orario, _, resto = m.groups()
    tipo = re.search(r"^((?:System|Microsoft)\.[\w.]+)", resto, re.M)
    messaggio = re.search(r"^(?:System|Microsoft)[\w.]+: (.{0,70})", resto, re.M)
    fotogramma = re.search(r"at (Vipi\.[\w.<>`]+)\(", resto)
    voci.append((orario, era(orario),
                 (tipo.group(1).split(".")[-1] if tipo else "-",
                  fotogramma.group(1) if fotogramma else "-",
                  messaggio.group(1) if messaggio else "")))

note = [(m.group(1), era(m.group(1)), m.group(2))
        for m in re.finditer(r"^NOTA (\S+ \S+) UTC · (.{0,90})", testo, re.M)]

print(f"\nVoci: {len(voci)}" + (f", dalla {voci[0][0]} alla {voci[-1][0]} UTC" if voci else ""))
# Le ere nell'ordine in cui compaiono in avvii.txt, anche quelle senza voci: un'era muta si deve VEDERE.
per_era = collections.OrderedDict((e, []) for e in ["(prima di avvii.txt)"] + [v for _, v in inizi])
for orario, e, famiglia in voci:
    per_era[e].append((orario, famiglia))
if not per_era["(prima di avvii.txt)"] and not any(v == "(prima di avvii.txt)" for _, v, _ in note):
    del per_era["(prima di avvii.txt)"]

for e, vs in per_era.items():
    traffico = (f" · {richieste[e]} richieste in {accensioni[e]} accensioni, {dal_ping[e]} svegliate dal ping"
                if accensioni[e] else "")
    finestra = f", {vs[0][0]} → {vs[-1][0]}" if vs else ""
    print(f"\n=== era {e}: {len(vs)} voci{finestra}{traffico}")
    for (tipo, fotogramma, messaggio), n in collections.Counter(f for _, f in vs).most_common(15):
        print(f"   {n:4d}  {tipo} · {fotogramma} · {messaggio}")
    for testo_nota, n in collections.Counter(t[:80] for _, v, t in note if v == e).most_common():
        print(f"   {n:4d}  NOTA · {testo_nota}")
