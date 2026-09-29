namespace Vipi.Application.Abstractions;

/// <summary>Com'è andata una sostituzione: chi è passato a chi, e quanti riferimenti per numero ha spostato.</summary>
public sealed record SostituzioneEsito(string Vecchio, string Nuovo, int Accordi, int Blocchi, int Figli, bool Documento);

/// <summary>
/// «Sostituisci con…» (S54, committente, 29 settembre 2026): una posizione IVAO è diventata un'altra, ma la sorgente
/// non l'ha detto — ha tolto la vecchia e ne ha creata una nuova, con un'identità diversa (LIRN_US0_APP →
/// LIRR_US0_APP). La rinomina automatica non la riconosce, e tutto restava sul settore vecchio, ormai orfano.
/// <para>Qui lo dice una persona, e succede quello che la rinomina automatica fa da sola: tutto ciò che puntava al
/// vecchio passa al nuovo — per numero (accordi, blocchi, vLOA, figli, documento, link di frequenza) e per nome
/// (gerarchia, ripieghi, agganci AIP, gruppi APP, configurazioni, chiavi di release, posizioni degli enti, alias).
/// Il vecchio resta un orfano senza più niente addosso, e si elimina come gli altri.</para>
/// </summary>
public interface ISectorSubstitution
{
    /// <summary>Non salva in una transazione sua: la apre il chiamante. Rifiuta PRIMA di scrivere se qualcosa non
    /// si può spostare senza scegliere al posto di una persona (un accordo che esiste già col nuovo, due documenti,
    /// due enti diversi).</summary>
    Task<SostituzioneEsito> SostituisciAsync(int vecchioSectorId, int nuovoSectorId, int actorUserId, CancellationToken ct = default);
}
