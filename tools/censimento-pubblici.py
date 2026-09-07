# -*- coding: utf-8 -*-
"""Censimento della superficie pubblica di un progetto: quali tipi NESSUNO nomina da fuori.

R-009 dice «censimento, non lista di cancellazioni», ed è la ragione per cui questo script si ferma qui:
propone, non tocca. La scansione non legge gli `.xaml`, non vede la riflessione e non sa che un tipo può
essere pubblico per una ragione che il nome non dice — quindi la decisione resta a chi legge.

Uso: python censimento-pubblici.py <progetto> [--escludi Migrations,Snapshot]
"""
import os
import re
import sys

DICHIARAZIONE = re.compile(
    r'(?m)^public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+)*'
    r'(?:class|interface|enum|struct|record(?:\s+struct)?)\s+(\w+)')


def sorgenti(radice):
    for r, _, fs in os.walk(radice):
        parti = r.split(os.sep)
        if 'obj' in parti or 'bin' in parti:
            continue
        for f in fs:
            if f.endswith(('.cs', '.razor')):
                yield os.path.join(r, f)


def main():
    progetto = sys.argv[1].rstrip('/\\')
    escludi = []
    if '--escludi' in sys.argv:
        escludi = sys.argv[sys.argv.index('--escludi') + 1].split(',')

    dichiarati = {}
    for p in sorgenti(progetto):
        if any(x in p for x in escludi):
            continue
        testo = open(p, encoding='utf-8-sig', errors='replace').read()
        for nome in DICHIARAZIONE.findall(testo):
            dichiarati.setdefault(nome, p)

    # Tutto il resto del repository: src, tests, tools — MENO il progetto stesso.
    altrove = []
    for radice in ('src', 'tests', 'tools'):
        if not os.path.isdir(radice):
            continue
        for p in sorgenti(radice):
            if os.path.abspath(p).startswith(os.path.abspath(progetto) + os.sep):
                continue
            altrove.append(open(p, encoding='utf-8-sig', errors='replace').read())
    fuori = "\n".join(altrove)

    mai_nominati = sorted(n for n in dichiarati if not re.search(r'\b' + re.escape(n) + r'\b', fuori))

    print(f"tipi pubblici dichiarati in {progetto}: {len(dichiarati)}")
    print(f"mai nominati da fuori: {len(mai_nominati)}")
    for n in mai_nominati:
        print(f"  {n:45s} {dichiarati[n].replace(os.sep, '/')}")


if __name__ == "__main__":
    main()
