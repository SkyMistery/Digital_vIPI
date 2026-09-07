namespace Vipi.Application.Content;

/// <summary>Dov'è una sezione comune dentro un documento dell'unione.</summary>
/// <param name="DocumentId">Il documento che la porta.</param>
/// <param name="SectionId">La sezione, nella sua versione di lavoro.</param>
/// <param name="Nascosta">Com'è adesso: serve a dire a chi guarda che cosa cambierà davvero.</param>
public sealed record PresenzaSezione(int DocumentId, int SectionId, bool Nascosta);

/// <summary>Una sezione che due o più documenti dell'unione hanno <b>tutti e due</b>.</summary>
/// <param name="Proposta">Nasce spuntata nella scheda. Falso per la validità — vedi
/// <see cref="SezioniComuni.ChiaveValidita"/>.</param>
public sealed record SezioneComune(string Chiave, string Titolo, IReadOnlyList<PresenzaSezione> Presenze,
                                   bool Proposta);

/// <summary>
/// Le sezioni <b>in comune</b> fra i documenti di un'unione, e che cosa nascondere quando si sceglie chi le
/// tiene (richiesta del committente, 7 settembre 2026).
///
/// <para>Unendo la vIPI d'aeroporto di uno scalo e il suo vSOP militare la pagina ripete METAR, frequenze,
/// piste e quote di transizione: due volte lo stesso dato, spesso scritto una volta sola bene. La scheda
/// chiede <b>chi le tiene</b> e nasconde le altre — con lo <b>stesso</b> flag del tasto «nascondi»
/// dell'editor, che resta l'unico modo di disfarlo.</para>
///
/// <para>
/// ⚠️ <b>Pura, e separata dal servizio, apposta</b>: è la parte che si può sbagliare senza che nulla
/// protesti — una chiave in più nell'elenco nasconde una sezione che qualcuno voleva vedere, e non lo dice
/// nessun errore. Provarla attraverso il servizio vorrebbe dire una fixture con archivio, lock e
/// autorizzazione per verificare un confronto di stringhe.
/// </para>
/// </summary>
public static class SezioniComuni
{
    /// <summary>
    /// «Validità e revisione»: comune per CHIAVE, non per significato.
    /// <para>⚠️ Dice ciclo AIRAC e release <b>di quel documento</b>, e in un'unione sono due — cicli
    /// indipendenti fino al giorno in cui qualcuno li ha accoppiati. Sta in elenco (chi la vuole nascondere
    /// può) ma <b>non spuntata</b>: nessuno la nasconde per distrazione.</para>
    /// </summary>
    public const string ChiaveValidita = "validity";

    /// <summary>
    /// Le sezioni comuni ai documenti dati, nell'ordine in cui compaiono nel <b>primo</b> (l'ospite).
    ///
    /// <para>⚠️ Il confronto è sulla <b>chiave di catalogo</b>, non sul titolo: nel vSOP militare le
    /// frequenze si chiamano «Frequenze ATC/CRC» e nella vIPI d'aeroporto «Frequenze», ma la chiave è
    /// <c>frequencies</c> in tutti e due. Le sezioni <b>libere</b> restano fuori per costruzione — la loro
    /// chiave nasce unica (<c>SectionKeys.NewCustom</c>), quindi due sezioni scritte a mano non si
    /// somigliano mai, nemmeno quando si chiamano uguale.</para>
    ///
    /// <para>⚠️ Si guarda anche <b>dentro</b>: nel vSOP frequenze, piste e quote di transizione sono
    /// <i>figlie</i> di «Dati generali», nella vIPI stanno in cima. Un confronto sui soli primi livelli non
    /// troverebbe niente proprio nel caso per cui la scheda esiste.</para>
    /// </summary>
    public static IReadOnlyList<SezioneComune> Di(
        IReadOnlyList<(int DocumentId, IReadOnlyList<EditableSection> Sezioni)> documenti)
    {
        var presenze = new Dictionary<string, List<PresenzaSezione>>(StringComparer.Ordinal);
        var titoli = new Dictionary<string, string>(StringComparer.Ordinal);
        var ordine = new List<string>();

        foreach (var (documentId, sezioni) in documenti)
            foreach (var s in Appiattisci(sezioni))
            {
                if (SectionKeys.IsCustom(s.SectionKey) || string.IsNullOrWhiteSpace(s.SectionKey)) continue;

                if (!presenze.TryGetValue(s.SectionKey, out var lista))
                {
                    presenze[s.SectionKey] = lista = new List<PresenzaSezione>();
                    titoli[s.SectionKey] = s.Title;
                    ordine.Add(s.SectionKey);
                }
                lista.Add(new PresenzaSezione(documentId, s.Id, s.IsHidden));
            }

        return ordine
            // Due DOCUMENTI, non due righe: la stessa chiave due volte nello stesso documento non è una
            // ripetizione fra documenti, ed è la domanda a cui la scheda risponde.
            .Where(k => presenze[k].Select(p => p.DocumentId).Distinct().Count() > 1)
            .Select(k => new SezioneComune(k, titoli[k], presenze[k], Proposta: k != ChiaveValidita))
            .ToList();
    }

    /// <summary>
    /// Che cosa scrivere davvero, scelte le chiavi e il documento che le <b>tiene</b>: le sue tornano
    /// visibili, quelle degli altri si nascondono. Le sezioni già a posto non compaiono — un piano vuoto
    /// vuol dire «non c'è niente da fare», ed è una risposta.
    ///
    /// <para>⚠️ Il documento che tiene si <b>mostra</b>, non si lascia com'è: senza, cambiare idea sul
    /// vincitore lascerebbe nascoste tutt'e due le copie — la seconda scelta nasconderebbe le sezioni
    /// dell'altro senza rimettere le proprie, e la pagina unita resterebbe senza METAR. Chi sceglie «le
    /// tiene la vIPI» sta dicendo che nella vIPI si vedono.</para>
    /// </summary>
    public static IReadOnlyList<(int SectionId, bool Nascondi)> Piano(
        IReadOnlyList<SezioneComune> comuni, IReadOnlyList<string> chiavi, int documentoCheTiene)
    {
        var scelte = new HashSet<string>(chiavi, StringComparer.Ordinal);
        return comuni
            .Where(c => scelte.Contains(c.Chiave))
            .SelectMany(c => c.Presenze)
            .Select(p => (p.SectionId, Nascondi: p.DocumentId != documentoCheTiene, p.Nascosta))
            .Where(x => x.Nascondi != x.Nascosta)
            .Select(x => (x.SectionId, x.Nascondi))
            .ToList();
    }

    /// <summary>
    /// Chi PROPORRE come documento che tiene, guardando lo stato: quello con meno sezioni comuni nascoste.
    /// A parità vince il primo dell'elenco, cioè l'ospite — ed è il caso di un'unione appena nata, dove
    /// nessuno ha ancora nascosto niente.
    ///
    /// <para>🔴 <b>Senza questo la scheda MENTE alla seconda apertura.</b> Riproponendo sempre l'ospite,
    /// chi riapre e preme «nascondi» senza guardare <b>ribalta</b> la scelta di prima: rimette le sezioni
    /// dell'uno e nasconde quelle dell'altro, con un conto che dice «22 cambiate» al posto di «nessuna».
    /// Misurato a schermo il 7 settembre 2026 — nessun errore, nessun rosso, e il documento che diceva
    /// un'altra cosa. Lo stato è già scritto nell'archivio: la domanda si fa a lui, non al valore di
    /// default.</para>
    /// </summary>
    public static int CheTiene(IReadOnlyList<SezioneComune> comuni, IReadOnlyList<int> membriInOrdine)
    {
        if (membriInOrdine.Count == 0) return 0;

        return membriInOrdine
            .Select((id, posizione) => (id, posizione,
                nascoste: comuni.Sum(c => c.Presenze.Count(p => p.DocumentId == id && p.Nascosta))))
            .OrderBy(x => x.nascoste)
            .ThenBy(x => x.posizione)
            .First().id;
    }

    /// <summary>L'albero delle sezioni letto per intero, padri e figli, nell'ordine in cui si legge.</summary>
    private static IEnumerable<EditableSection> Appiattisci(IReadOnlyList<EditableSection> sezioni)
    {
        foreach (var s in sezioni)
        {
            yield return s;
            foreach (var f in Appiattisci(s.Children)) yield return f;
        }
    }
}
