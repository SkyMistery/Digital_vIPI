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

  return { osserva };
})();
