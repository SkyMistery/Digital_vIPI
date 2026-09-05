// Ridimensionamento delle immagini NEL BROWSER, prima che partano.
//
// Perché: una foto scattata col telefono pesa 5-10 MB e supererebbe il limite di caricamento, costringendo chi
// scrive a rimpicciolirla a mano; ridurla qui la fa passare, alleggerisce le pagine pubbliche e — soprattutto —
// evita di aggiungere una libreria di imaging server-side che dovrebbe decodificare un file non fidato.
//
// COME, e perché così: si intercetta l'evento `change` del file input in fase di CATTURA, lo si ferma, si ricodifica
// e si ri-emette con il file rimpicciolito al posto dell'originale. Blazor vede quindi un solo `change`, con dentro
// già il file giusto, e il codice C# non deve sapere niente di tutto questo — legge il file come sempre.
// L'alternativa (restituire i byte a .NET come IJSStreamReference) è stata provata e scartata il 2026-07-31:
// `input.files` non è più leggibile quando .NET richiama, e uno stream creato dentro una funzione asincrona arriva
// a Blazor senza il blob dietro («Supplied value is not a typed array or blob»). Falliva in silenzio, caricando
// ogni foto a piena misura.
window.vipiMedia = (() => {

  // Il GIF resta fuori: ridisegnarlo su canvas terrebbe solo il primo fotogramma, buttando l'animazione.
  const encodableAs = (type) =>
    type === 'image/png' ? 'image/png' :
    type === 'image/webp' ? 'image/webp' :
    type === 'image/jpeg' ? 'image/jpeg' : null;

  const conEstensione = (nome, tipo) =>
    nome.replace(/\.[^.]*$/, '') + (tipo === 'image/webp' ? '.webp' : tipo === 'image/png' ? '.png' : '.jpg');

  /// Restituisce il blob rimpicciolito, o null quando NON conviene toccare nulla (e allora passa l'originale).
  async function ridimensiona(file, maxSide, quality, maxBytes) {
    const type = encodableAs(file.type);
    if (!type || !maxSide || maxSide <= 0) return null;

    const bitmap = await createImageBitmap(file);
    const side = Math.max(bitmap.width, bitmap.height);
    if (side <= maxSide) { if (bitmap.close) bitmap.close(); return null; }   // già dentro misura

    const scale = maxSide / side;
    const w = Math.max(1, Math.round(bitmap.width * scale));
    const h = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    canvas.getContext('2d').drawImage(bitmap, 0, 0, w, h);
    if (bitmap.close) bitmap.close();

    const encode = (t) => new Promise(resolve => canvas.toBlob(resolve, t, quality));
    const troppoGrande = (b) => maxBytes > 0 && b.size > maxBytes;

    // Primo tentativo: stesso formato dell'originale, così uno schema o uno screenshot PNG resta senza perdite.
    const candidati = [];
    const stessoFormato = await encode(type);
    if (stessoFormato) candidati.push(stessoFormato);

    // Il WebP si prova solo se il primo tentativo non basta: o non ha guadagnato niente (capita coi PNG molto
    // "rumorosi") o resta comunque sopra il limite. Meglio un'immagine leggermente compressa di un rifiuto.
    if (!stessoFormato || troppoGrande(stessoFormato) || stessoFormato.size >= file.size) {
      const webp = await encode('image/webp');
      if (webp) candidati.push(webp);
    }

    candidati.sort((a, b) => a.size - b.size);
    const migliore = candidati[0];

    // Se nemmeno il più piccolo batte l'originale, l'originale era già ottimizzato: si carica quello.
    return migliore && migliore.size < file.size ? migliore : null;
  }

  /// Aggancia l'intercettazione a un file input. Idempotente: la si può richiamare a ogni render.
  function osserva(input, maxSide, quality, maxBytes) {
    if (!input || input.__vipiOsservato) return;
    input.__vipiOsservato = true;

    input.addEventListener('change', (e) => {
      if (input.__vipiRiemesso) { input.__vipiRiemesso = false; return; }   // è il nostro: lascialo salire

      const file = input.files && input.files[0];
      if (!file || !encodableAs(file.type)) return;    // formato che non sappiamo ricodificare: passa com'è

      // Da qui in poi l'evento è nostro: Blazor non deve leggere il file finché non abbiamo deciso quale sia.
      e.stopImmediatePropagation();

      const riemetti = () => {
        input.__vipiRiemesso = true;
        input.dispatchEvent(new Event('change', { bubbles: true }));
      };

      ridimensiona(file, maxSide, quality, maxBytes)
        .then((blob) => {
          if (blob) {
            const dt = new DataTransfer();
            dt.items.add(new File([blob], conEstensione(file.name, blob.type), { type: blob.type }));
            input.files = dt.files;
          }
          riemetti();
        })
        // Formato che il browser non sa decodificare, canvas "sporcato", memoria: si carica l'originale.
        .catch(riemetti);
    }, true);
  }

  // ── Maniglia di ridimensionamento ────────────────────────────────────────────────────────────────────────
  //
  // Che cosa si conserva: una PERCENTUALE della colonna, non dei pixel (la stessa immagine si legge su un monitor,
  // su un telefono e su un A4). Quindi il trascinamento si misura in pixel ma si chiude in percentuale.
  //
  // Perché il .NET lo sente una volta sola: durante il trascinamento la larghezza la scrive il browser sull'elemento
  // (nessun round-trip sul circuito, nessun salvataggio per pixel mosso); a dito alzato parte UNA chiamata e il C#
  // salva. Se il render che segue riscrive lo stesso `style="width:N%"`, riscrive esattamente ciò che c'è già.
  //
  // La figura NON arriva da .NET: la trova la maniglia risalendo il DOM. Passarle tutt'e due da Blazor vuol dire
  // un `@ref` sull'elemento reso da un ALTRO componente (ImageFigure), che il chiamante non ha; il 5 settembre 2026
  // il primo tentativo passava la maniglia due volte e la verifica live ha mostrato il bottone che si stringeva
  // mentre l'immagine restava intera. Un elemento solo, e la parentela la dice il DOM.
  function ridimensionabile(maniglia, minimo, dotNet, metodo) {
    if (!maniglia || maniglia.__vipiManiglia) return;
    const figura = maniglia.closest('figure.doc-img');
    if (!figura) return;
    maniglia.__vipiManiglia = true;

    const etichetta = figura.querySelector('.img-size');
    const min = minimo > 0 ? minimo : 10;
    let trascinando = null;

    // Larghezza UTILE della colonna: `clientWidth` comprende il padding, e nell'editor la figura sta dentro un
    // riquadro imbottito — contarlo darebbe una percentuale piu' piccola del vero, e a schermo l'immagine
    // sembrerebbe rimpicciolirsi da sola passando dall'editor al documento.
    const colonnaUtile = (colonna) => {
      const cs = getComputedStyle(colonna);
      return colonna.clientWidth - (parseFloat(cs.paddingLeft) || 0) - (parseFloat(cs.paddingRight) || 0);
    };

    const percentuale = (px, larghezzaColonna) =>
      Math.min(100, Math.max(min, Math.round((px / larghezzaColonna) * 100)));

    const mostra = (pct) => { if (etichetta) etichetta.textContent = pct + '%'; };

    const applica = (pct) => {
      figura.style.width = pct === 100 ? '' : pct + '%';
      mostra(pct);
    };

    maniglia.addEventListener('pointerdown', (e) => {
      // La colonna è il genitore della figura: è lo spazio di cui la percentuale è una frazione, ed è lo stesso
      // rapporto che il viewer applicherà nella SUA colonna, di qualunque larghezza sia.
      const colonna = figura.parentElement;
      if (!colonna) return;
      const larghezza = colonnaUtile(colonna);
      if (larghezza <= 0) return;

      e.preventDefault();
      e.stopPropagation();          // la figura sta dentro un <label> con sopra il file input: senza questo, il
                                    // trascinamento aprirebbe anche la finestra «scegli un file».
      trascinando = { x: e.clientX, w: figura.getBoundingClientRect().width, colonna: larghezza, pct: null };
      figura.classList.add('sizing');
      mostra(percentuale(trascinando.w, larghezza));
      maniglia.setPointerCapture(e.pointerId);
    });

    maniglia.addEventListener('pointermove', (e) => {
      if (!trascinando) return;
      trascinando.pct = percentuale(trascinando.w + (e.clientX - trascinando.x), trascinando.colonna);
      applica(trascinando.pct);
    });

    const chiudi = (e) => {
      if (!trascinando) return;
      const pct = trascinando.pct;
      trascinando = null;
      figura.classList.remove('sizing');
      if (maniglia.hasPointerCapture(e.pointerId)) maniglia.releasePointerCapture(e.pointerId);
      // Nessun movimento = nessun salvataggio: un clic sulla maniglia non deve sporcare il documento.
      if (pct !== null) dotNet.invokeMethodAsync(metodo, pct);
    };

    maniglia.addEventListener('pointerup', chiudi);
    // Trascinamento interrotto (Esc, dito uscito dallo schermo, browser che revoca la cattura): si chiude come
    // sopra — la misura che si vede è quella che si salva, o resterebbe a schermo una larghezza che nessuno ha.
    maniglia.addEventListener('pointercancel', chiudi);

    // Tastiera: la maniglia è un <button>, quindi ci si arriva col tab. Frecce = 5 punti per volta, perché una
    // funzione che si può usare SOLO trascinando non è usabile da chi non usa il mouse.
    maniglia.addEventListener('keydown', (e) => {
      const passo = e.key === 'ArrowRight' || e.key === 'ArrowUp' ? 5
                  : e.key === 'ArrowLeft' || e.key === 'ArrowDown' ? -5 : 0;
      if (passo === 0) return;
      e.preventDefault();
      const colonna = figura.parentElement;
      const larghezza = colonna ? colonnaUtile(colonna) : 0;
      if (larghezza <= 0) return;
      const attuale = percentuale(figura.getBoundingClientRect().width, larghezza);
      const pct = Math.min(100, Math.max(min, attuale + passo));
      applica(pct);
      figura.classList.add('sizing');
      dotNet.invokeMethodAsync(metodo, pct);
    });
    maniglia.addEventListener('blur', () => figura.classList.remove('sizing'));
  }

  return { osserva, ridimensionabile };
})();
