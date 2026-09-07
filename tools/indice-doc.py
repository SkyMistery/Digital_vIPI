# -*- coding: utf-8 -*-
"""Rigenera l'elenco COMPLETO delle carte in `docs/index.md`, fra i due marcatori.

Perché esiste: l'indice si apre dicendo «mappa di TUTTI i documenti del progetto», e al 7 settembre 2026 ne
mancavano 44 su 166 — trentatré dei quali carte di funzionalità, cioè il posto dove sta scritto PERCHÉ una
cosa è fatta così. Il difetto non era l'elenco incompleto: era che si dichiarasse completo (revisione del
6 settembre 2026, R-030 — stessa specie di R-012, la lista migrazioni «autoritativa» ferma all'85ª di 114).

⚠️ Un elenco che si aggiorna a mano ricade sempre. Le sezioni curate dell'indice restano scritte da una
persona — sono la guida alla LETTURA, e quella è un giudizio; questo elenco no: è un fatto, e i fatti li
scrive un comando.

Uso:
    python tools/indice-doc.py            # riscrive la sezione generata
    python tools/indice-doc.py --prova    # non scrive: esce 1 se la sezione sarebbe diversa
"""
import os
import sys

INIZIO = "<!-- ELENCO GENERATO: non scrivere a mano fra questi due marcatori (tools/indice-doc.py) -->"
FINE = "<!-- FINE ELENCO GENERATO -->"

INTESTAZIONE = """## Tutte le carte, per cartella

Generato da `tools/indice-doc.py`: c'è **ogni** file di `docs/`, comprese quelle che le sezioni curate qui
sopra non nominano. Le sezioni sopra dicono *cosa leggere*; questo elenco dice *cosa c'è*."""


def radice() -> str:
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def carte(base: str):
    for cartella, _, files in os.walk(os.path.join(base, "docs")):
        for f in files:
            if f.endswith(".md"):
                rel = os.path.relpath(os.path.join(cartella, f), os.path.join(base, "docs"))
                yield rel.replace(os.sep, "/")


def titolo_di(percorso: str) -> str:
    """Il primo titolo del file: è quello che dice a cosa serve, meglio del nome."""
    try:
        with open(percorso, encoding="utf-8") as f:
            for riga in f:
                r = riga.strip().lstrip("﻿")
                if r.startswith("# ") or r.startswith("## "):
                    return r.lstrip("#").strip()
    except OSError:
        pass
    return os.path.basename(percorso)[:-3]


def blocco(base: str) -> str:
    righe = [INIZIO, "", INTESTAZIONE, ""]
    ultima = None

    for rel in sorted(carte(base)):
        if rel == "index.md":
            continue

        cartella = rel.split("/")[0] if "/" in rel else "(radice)"
        if cartella != ultima:
            righe += ["", f"### `{cartella}`", ""]
            ultima = cartella

        titolo = titolo_di(os.path.join(base, "docs", rel))
        righe.append(f"- [`{rel}`]({rel}) — {titolo}")

    righe += ["", FINE]
    return "\n".join(righe)


def main() -> int:
    base = radice()
    indice = os.path.join(base, "docs", "index.md")
    testo = open(indice, encoding="utf-8").read()
    nuovo_blocco = blocco(base)

    if INIZIO in testo and FINE in testo:
        a = testo.index(INIZIO)
        b = testo.index(FINE) + len(FINE)
        nuovo = testo[:a] + nuovo_blocco + testo[b:]
    else:
        nuovo = testo.rstrip() + "\n\n---\n\n" + nuovo_blocco + "\n"

    if "--prova" in sys.argv:
        if nuovo != testo:
            print("indice-doc: l'elenco generato NON è aggiornato. Rigenerarlo con `python tools/indice-doc.py`.")
            return 1
        print("indice-doc: elenco aggiornato.")
        return 0

    open(indice, "w", encoding="utf-8", newline="").write(nuovo)
    print(f"indice-doc: elenco generato ({sum(1 for _ in carte(base))} carte).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
