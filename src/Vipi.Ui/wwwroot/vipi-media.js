// Ridimensionamento delle immagini NEL BROWSER, prima che partano.
//
// Perché qui e non sul server: una foto scattata col telefono pesa 5-10 MB e supererebbe il limite di caricamento,
// costringendo chi scrive a rimpicciolirla a mano; ridurla qui la fa passare, riduce il traffico e — soprattutto —
// evita di aggiungere una libreria di imaging server-side che dovrebbe decodificare un file non fidato.
//
// Restituisce uno stream leggibile da .NET (chunked su SignalR), oppure null quando NON conviene toccare nulla:
// in quel caso il chiamante carica il file originale e il limite lo fa comunque rispettare il server.
window.vipiMedia = (() => {

  // Il GIF resta fuori: ridisegnarlo su canvas terrebbe solo il primo fotogramma, buttando l'animazione.
  const encodableAs = (type) =>
    type === 'image/png' ? 'image/png' :
    type === 'image/webp' ? 'image/webp' :
    type === 'image/jpeg' ? 'image/jpeg' : null;

  async function downscale(input, maxSide, quality) {
    try {
      const file = input && input.files && input.files[0];
      if (!file || !maxSide || maxSide <= 0) return null;

      const type = encodableAs(file.type);
      if (!type) return null;

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

      const blob = await new Promise(resolve => canvas.toBlob(resolve, type, quality));
      // Se la ricodifica non ha guadagnato niente (capita coi PNG piatti), meglio l'originale: è già ottimizzato.
      if (!blob || blob.size >= file.size) return null;

      return DotNet.createJSStreamReference(blob);
    } catch {
      // Formato che il browser non sa decodificare, canvas "sporcato", memoria: si carica l'originale.
      return null;
    }
  }

  return { downscale };
})();
