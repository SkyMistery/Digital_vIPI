# Third-party notices

Questo prodotto è distribuito sotto **Apache License 2.0** (vedi `LICENSE`), ma **ridistribuisce**
componenti di terzi che restano soggetti alla propria licenza. Elenco autorevole: se aggiungi un file
di terzi sotto `src/Vipi.Ui/wwwroot/`, va aggiunto anche qui.

---

## three.js

- **File:** `src/Vipi.Ui/wwwroot/vendor/three.min.js`
- **Copyright:** © 2010–2021 three.js authors
- **Licenza:** MIT
- **Sito:** https://threejs.org — https://github.com/mrdoob/three.js

Caricato su richiesta dal viewer AoR 3D (`vipi-aor3d.js` lo legge da `data-three-src`), non nel `<head>`.

```
The MIT License

Copyright © 2010-2021 three.js authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

---

## Nunito Sans

- **File:** `src/Vipi.Ui/wwwroot/fonts/pe0TMImSLYBIv1o4X1M8*.woff2` (subset latin/latin-ext)
- **Copyright:** © 2016 The Nunito Sans Project Authors
- **Licenza:** SIL Open Font License 1.1
- **Sito:** https://fonts.google.com/specimen/Nunito+Sans

## Poppins

- **File:** `src/Vipi.Ui/wwwroot/fonts/pxiByp8kv8JHgFVr*.woff2`, `pxiEyp8kv8JHgFVr*.woff2`
- **Copyright:** © 2014 Indian Type Foundry
- **Licenza:** SIL Open Font License 1.1
- **Sito:** https://fonts.google.com/specimen/Poppins

Entrambi i font sono **self-hosted** (sottoinsieme generato da Google Fonts) e dichiarati in
`vipi-fonts.css`. La OFL richiede che questa nota accompagni i file ridistribuiti; il testo completo
della licenza è su https://openfontlicense.org (SIL OFL 1.1). Nessuno dei due font è venduto da solo
né usa i nomi riservati previsti dalla OFL.

---

## Leaflet

- **File:** `src/Vipi.Ui/wwwroot/vendor/leaflet/leaflet.js`, `leaflet.css`, `images/*.png`
- **Copyright:** © 2010–2023 Volodymyr Agafonkin, © 2010–2011 CloudMade
- **Licenza:** BSD-2-Clause
- **Sito:** https://leafletjs.com — https://github.com/Leaflet/Leaflet

⚠️ **Dall'11 agosto 2026 Leaflet è RIDISTRIBUITO, e questa sezione è cambiata di conseguenza.** Fino a
quel giorno arrivava dalla CDN unpkg e qui c'era scritto «non ridistribuito, nessun obbligo di notice».
Vendorizzandolo l'obbligo lo abbiamo acquisito noi — è la ragione per cui il testo della licenza sta qui
sotto per esteso, come per three.js e per i font.

Caricato in `src/Vipi.Host/Components/App.razor` dalle rotte statiche del modulo. ℹ️ Le **tessere** della
mappa restano esterne (`basemaps.cartocdn.com`): quelle non si vendorizzano, e sono l'unico host di terzi
che la pagina contatti ancora.

```
BSD 2-Clause License

Copyright (c) 2010-2023, Volodymyr Agafonkin
Copyright (c) 2010-2011, CloudMade
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are
permitted provided that the following conditions are met:

   1. Redistributions of source code must retain the above copyright notice, this list of
      conditions and the following disclaimer.

   2. Redistributions in binary form must reproduce the above copyright notice, this list
      of conditions and the following disclaimer in the documentation and/or other materials
      provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS
OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
