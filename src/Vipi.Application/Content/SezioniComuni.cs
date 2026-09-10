using Vipi.Domain;

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
    /// Chi <b>partecipa</b> al confronto, fra i membri di un'unione: i documenti che descrivono lo
    /// <b>STESSO LUOGO</b> — la vIPI d'aeroporto e il vSOP militare di quello scalo. Meno di due che si
    /// confrontano = nessuna sezione in comune, e la scheda non ha niente da chiedere.
    ///
    /// <para>
    /// 🔴 <b>Segnalato dal committente il 9 settembre 2026</b>, unendo un terzo documento
    /// (<c>LIBV_APP</c>) alla vIPI e al vSOP di Gioia del Colle: la scheda si apriva e proponeva di
    /// nascondere sezioni che <b>non sono ripetizioni</b>.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>La stessa chiave non vuol dire lo stesso dato.</b> Misurato sul catalogo: un APP e la vIPI
    /// d'aeroporto hanno tre chiavi in comune — <c>frequencies</c>, <c>operationaltechnique</c>,
    /// <c>validity</c>. Ma le «Frequenze» di un APP sono quelle dell'<b>avvicinamento</b> e quelle
    /// dell'aeroporto sono del <b>campo</b>: non c'è niente di ripetuto da togliere, e nasconderne una
    /// <b>perde contenuto vero</b>. La scheda le proponeva pure <b>già spuntate</b>: chi premeva senza
    /// guardare le nascondeva.
    /// </para>
    ///
    /// <para>
    /// Vale anche fra due APP dello stesso campo (LIBV ne ha due): settori diversi, frequenze diverse.
    /// L'unico asse su cui una chiave uguale significa davvero la stessa cosa è il <b>luogo</b>.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Sta <b>qui</b> e non nel pannello: il pannello è l'unico chiamante di oggi, e una regola
    /// editoriale scritta dentro chi la usa è una regola che il secondo chiamante non troverà. La porta
    /// del servizio la applica per conto suo, così nessuno la può scavalcare passando degli id nudi.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> Confrontabili(
        IReadOnlyList<(int DocumentId, ReleaseTargetType Famiglia)> membri)
    {
        var stessoLuogo = membri
            .Where(m => m.Famiglia is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil)
            .Select(m => m.DocumentId)
            .Distinct()
            .ToList();

        // Uno solo non ha con chi avere qualcosa in comune. Tornare quell'uno farebbe una scheda con un
        // documento e zero sezioni: una domanda senza risposte possibili.
        return stessoLuogo.Count > 1 ? stessoLuogo : Array.Empty<int>();
    }

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
    /// Che cosa scrivere davvero: le sezioni con queste chiavi si <b>nascondono</b> nei documenti scelti e
    /// si <b>mostrano</b> in tutti gli altri. Le sezioni già a posto non compaiono nel piano — un piano
    /// vuoto vuol dire «non c'è niente da fare», ed è una risposta.
    ///
    /// <para>⚠️ Chi NON è selezionato si <b>mostra</b>, non si lascia com'è: senza, cambiare idea su quale
    /// documento nascondere lascerebbe nascoste tutt'e due le copie — la seconda scelta nasconderebbe
    /// l'altro senza rimettere il primo, e la pagina unita resterebbe senza METAR.</para>
    ///
    /// <para>⚠️ La polarità è quella chiesta dal committente il 7 settembre 2026: <b>si sceglie il documento
    /// da cui SPARISCONO</b>. La prima stesura chiedeva chi le TIENE — la stessa scheda, letta al
    /// contrario — ed è esattamente il clic sbagliato che aspetta di succedere.</para>
    /// </summary>
    public static IReadOnlyList<(int SectionId, bool Nascondi)> Piano(
        IReadOnlyList<SezioneComune> comuni, IReadOnlyList<string> chiavi, IReadOnlyCollection<int> nascondiIn)
    {
        var scelte = new HashSet<string>(chiavi, StringComparer.Ordinal);
        var da = new HashSet<int>(nascondiIn);
        return comuni
            .Where(c => scelte.Contains(c.Chiave))
            .SelectMany(c => c.Presenze)
            .Select(p => (p.SectionId, Nascondi: da.Contains(p.DocumentId), p.Nascosta))
            .Where(x => x.Nascondi != x.Nascosta)
            .Select(x => (x.SectionId, x.Nascondi))
            .ToList();
    }

    /// <summary>
    /// Vero se, con queste scelte, almeno una sezione comune <b>sparirebbe da tutti</b> i documenti che la
    /// portano: legittimo, ma va detto — è l'unico caso in cui la pagina unita perde del tutto un dato che
    /// c'era due volte.
    /// </summary>
    public static bool SparisceDaTutti(IReadOnlyList<SezioneComune> comuni, IReadOnlyList<string> chiavi,
                                       IReadOnlyCollection<int> nascondiIn)
    {
        var scelte = new HashSet<string>(chiavi, StringComparer.Ordinal);
        var da = new HashSet<int>(nascondiIn);
        return comuni.Where(c => scelte.Contains(c.Chiave))
                     .Any(c => c.Presenze.All(p => da.Contains(p.DocumentId)));
    }

    /// <summary>
    /// Da quali documenti PROPORRE di nascondere, guardando lo stato: quelli che hanno già nascosta almeno
    /// una sezione comune. Se non ne ha nascosta nessuno — unione appena nata — si propongono <b>tutti
    /// tranne il primo</b> dell'ordine memorizzato. ⚠️ Dalla §13 quel primo non è «l'ospite» di niente: è
    /// solo una proposta STABILE, la stessa da qualunque porta si apra la scheda, e resta modificabile.
    ///
    /// <para>🔴 <b>Senza guardare lo stato la scheda MENTE alla seconda apertura.</b> Riproponendo sempre lo
    /// stesso insieme, chi riapre e preme senza guardare <b>ribalta</b> la scelta di prima: rimette le
    /// sezioni dell'uno e nasconde quelle dell'altro, con un conto che dice «22 cambiate» al posto di
    /// «nessuna». Misurato a schermo il 7 settembre 2026 — nessun errore, nessun rosso, e il documento che
    /// diceva un'altra cosa.</para>
    /// </summary>
    public static IReadOnlyList<int> DoveNascondere(IReadOnlyList<SezioneComune> comuni,
                                                    IReadOnlyList<int> membriInOrdine)
    {
        if (membriInOrdine.Count == 0) return Array.Empty<int>();

        var conNascoste = membriInOrdine
            .Where(id => comuni.Any(c => c.Presenze.Any(p => p.DocumentId == id && p.Nascosta)))
            .ToList();

        return conNascoste.Count > 0 ? conNascoste : membriInOrdine.Skip(1).ToList();
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
