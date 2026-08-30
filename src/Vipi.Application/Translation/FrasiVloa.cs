using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Application.Translation;

/// <summary>
/// Mette in memoria l'italiano delle <b>frasi di partenza di una vLOA</b>, che sono parola nostra
/// (<see cref="VloaSections.FrasiDaSeminare"/>). Gemello di <see cref="TitoliUfficiali"/>, e per la stessa
/// ragione, un gradino più in là.
///
/// <para><b>Perché.</b> Quelle frasi le scriviamo noi, in inglese, quando una vLOA nasce. Mandarle a un
/// traduttore a pagamento è un errore in due modi: si <b>paga</b> per una risposta che sappiamo già, e la si
/// compra <b>sbagliata</b> — nessuno riguarda la resa automatica di un testo che sembra corretto.</para>
///
/// <para>⚠️ <b>E ce n'è una terza, misurata.</b> Due di quelle frasi hanno <b>due segnaposti attaccati</b>
/// (<c>LIBB/LGGG</c>), che è il costrutto che un motore tende a fondere. Il 30 agosto 2026 una tornava rotta
/// <b>a ogni giro</b>: scartata per non mettere in memoria una frase senza il callsign, quindi mai salvata,
/// quindi rispedita il quarto d'ora dopo — <b>155 caratteri buttati ogni quindici minuti</b>, circa 446 000
/// al mese, per una frase di cui conoscevamo la traduzione. Vedi lavori aperti §Q16b.</para>
///
/// <para>⚠️ <b>Le coppie di ACC sono quelle dei confinanti</b>, che è la stessa sorgente da cui la vLOA è
/// nata (<c>EfNeighbourRepository</c> chiama <c>VloaSections.Canonical</c> con quei tre campi): il testo
/// seminato è quindi <b>identico</b> a quello che sta nei documenti, e l'impronta corrisponde. Ricavare le
/// coppie in un altro modo vorrebbe dire seminare frasi che nessun documento contiene.</para>
/// </summary>
public static class FrasiVloa
{
    /// <summary>
    /// Semina le rese italiane mancanti. Idempotente: dal secondo giro non scrive niente.
    /// <para>⚠️ Il verso è <b>en→it</b>, al contrario dei titoli: una vLOA nasce <b>in inglese</b>.</para>
    /// </summary>
    public static async Task<int> SeminaAsync(
        ITranslationMemory memoria, INeighbourRepository confinanti, CancellationToken ct = default)
    {
        // ⚠️ Le impronte UMANE, non tutte: se un giro precedente ha già comprato una resa sbagliata, è
        // proprio quella che va sostituita. Stessa scelta di TitoliUfficiali, e per lo stesso motivo.
        // La copia in un insieme scrivibile serve al secondo motivo scritto piu' sotto: le frasi fisse sono
        // le stesse per tutte le coppie, e senza segnarle si riscriverebbero una volta per vLOA.
        var giaCi = new HashSet<string>(
            await memoria.LoadHumanHashesAsync("en", "it", ct).ConfigureAwait(false), StringComparer.Ordinal);
        var scritte = 0;

        foreach (var c in await confinanti.ListCandidatesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var (en, it) in VloaSections.FrasiDaSeminare(c.HomeAccCode, c.ForeignAccCode, c.ForeignAccName))
            {
                if (giaCi.Contains(TranslationText.Hash(en))) continue;

                // reviewerUserId 0 = nessuna persona: è l'originale del documento di partenza, non la
                // correzione di qualcuno. La pagina di revisione lo mostra come già rivisto, ed è giusto.
                await memoria.SaveHumanAsync("en", "it", en, it, reviewerUserId: 0, ct).ConfigureAwait(false);

                // ⚠️ Anche in memoria, o due coppie che condividono una frase fissa la scriverebbero due
                // volte: le ultime tre frasi non hanno parametri e sono uguali per tutte le vLOA.
                giaCi.Add(TranslationText.Hash(en));
                scritte++;
            }
        }

        return scritte;
    }
}
