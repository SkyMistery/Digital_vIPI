using Vipi.Application.Abstractions;

namespace Vipi.Application.Translation;

/// <summary>
/// L'italiano di alcune <b>descrizioni di aree IVAO</b> che il motore non riesce a tradurre, messo in memoria come
/// traduzione umana. Terzo gemello di <see cref="TitoliUfficiali"/> e <see cref="FrasiVloa"/>.
///
/// <para><b>Perché (§A64.3, misurato con Azure il 18 settembre 2026).</b> Le tre aree D del 37° Stormo — Marettimo,
/// Cielo campo, Mazara — scrivono «37th WING A/A TRAINING AREA» in maiuscolo. In prosa ogni parola maiuscola di
/// quattro lettere o più si protegge come sigla (potrebbe essere un punto: HOTRY, PORTO), quindi WING, TRAINING e
/// AREA partono in un segnaposto; Azure le traduce lo stesso (STORMO, ADDESTRAMENTO, ZONA), il ripristino vede un
/// identificatore cambiato e butta la frase. Non salvata, il giro dopo la rispedisce: <b>497 caratteri ogni quarto
/// d'ora</b>, per sempre.</para>
///
/// <para>⚠️ <b>Perché qui e non a mano.</b> Il testo viene dall'API IVAO (<c>/v2/specialAreas</c>) e ogni import lo
/// riscrive: la sorgente non si corregge. E una traduzione a mano non ha una porta: il Registro elenca le sole frasi
/// già in memoria, il glossario rifiuta i testi con identificatori.</para>
///
/// <para>⚠️ <b>Il sorgente è copiato BYTE PER BYTE dall'archivio</b> (copia di produzione del 18 settembre 2026),
/// punto elenco «•», tabulazione e spazi in coda compresi: l'impronta si calcola sul testo normalizzato, e un
/// carattere diverso darebbe un seme che nessun segmento cerca. Se IVAO cambia la descrizione il seme diventa
/// inerte, non sbagliato: il giro ricomincia a chiederla al motore.</para>
/// </summary>
public static class FrasiAreeIvao
{
    private const string Riga37 = "•\t37th WING A/A TRAINING AREA";
    private const string Riga37It = "•\tAREA ADDESTRAMENTO A/A DEL 37° STORMO";

    /// <summary>Le coppie inglese (com'è in archivio) → italiano.</summary>
    public static readonly IReadOnlyList<(string En, string It)> Frasi = new[]
    {
        // Marettimo
        (Riga37, Riga37It),

        // Cielo campo
        (Riga37 + " \r\n•\tNot avlb with all SIDs via TRP from LICJ \r\n•\tIncompatible with all the STAR NDB/VOR",
         Riga37It + " \r\n•\tNon disponibile con tutte le SID via TRP da LICJ \r\n•\tIncompatibile con tutte le STAR NDB/VOR"),

        // Mazara
        (Riga37 + " \r\n•\tIncompatible with Hi-Tacan RWY 31L\r\n•\tIncompatibile with the follwing STARs:"
             + "\r\n-\tPAL1F, PIVOP1F e MEGAN1F RWY 13R\r\n-\tPAL1A, PIVOP1A e MEGAN1A RWY 31L",
         Riga37It + " \r\n•\tIncompatibile con Hi-Tacan RWY 31L\r\n•\tIncompatibile con le seguenti STAR:"
             + "\r\n-\tPAL1F, PIVOP1F e MEGAN1F RWY 13R\r\n-\tPAL1A, PIVOP1A e MEGAN1A RWY 31L"),
    };

    /// <summary>
    /// Semina le rese italiane mancanti, verso <b>en→it</b> (le descrizioni IVAO sono in inglese). Idempotente, e
    /// non tocca una resa umana già in memoria: se una persona l'ha corretta, vince la sua.
    /// </summary>
    public static async Task<int> SeminaAsync(ITranslationMemory memoria, CancellationToken ct = default)
    {
        var giaCi = await memoria.LoadHumanHashesAsync("en", "it", ct).ConfigureAwait(false);
        var scritte = 0;

        foreach (var (en, it) in Frasi)
        {
            if (giaCi.Contains(TranslationText.Hash(en))) continue;
            await memoria.SaveHumanAsync("en", "it", TranslationText.Normalize(en), TranslationText.Normalize(it),
                reviewerUserId: 0, ct).ConfigureAwait(false);
            scritte++;
        }

        return scritte;
    }
}
