using Vipi.Application.Abstractions;

namespace Vipi.Application.Translation;

/// <summary>
/// I titoli delle sezioni che hanno un <b>originale ufficiale</b>, non una traduzione da indovinare.
///
/// <para>
/// ⚠️ <b>Perché esiste.</b> Il 28 agosto 2026, alla prima lettura in inglese di un vSOP militare, la
/// macchina ha reso «Piste» con <i>Slopes</i> e «Quote di transizione» con <i>Transition Dimensions</i>.
/// Non sono sfumature: sono i titoli sbagliati di due sezioni che parlano di piste e di livelli di
/// transizione, e un controllore che li legge così non trova quello che cerca. La macchina non poteva
/// saperlo — «pista» è anche una pista da sci — ma <b>noi sì</b>: quei titoli vengono dai quindici SOP
/// veri, che sono scritti in inglese, e l'originale sta nella carta.
/// </para>
///
/// <para>
/// ⚠️ Si seminano come <b>Human</b>, non come Machine: sono l'originale, non una proposta. Così nessun giro
/// li ritraduce, la pagina di revisione non li elenca fra le cose da guardare, e una correzione umana
/// successiva resta l'unica cosa che può cambiarli.
/// </para>
///
/// <para>
/// È il primo pezzo del <i>glossario di fraseologia</i> di cui parla <c>lavori-aperti §Q3</c>. Non lo
/// chiude — chi cura il glossario resta una domanda aperta — ma toglie di mezzo il caso in cui la risposta
/// giusta era già scritta e la stavamo buttando via.
/// </para>
/// </summary>
public static class TitoliUfficiali
{
    /// <summary>
    /// Italiano → inglese, dai quindici SOP militari (carta <c>2026-08-27-vsop-militari.md</c> §2).
    /// ⚠️ Solo i titoli che nel PDF ci sono davvero: <c>weather</c>, <c>transition</c> e <c>qra</c> sono
    /// aggiunte nostre e il loro inglese lo scriviamo noi, quindi stanno qui per lo stesso motivo — è la
    /// nostra parola, non una resa automatica.
    /// </summary>
    public static readonly IReadOnlyList<(string It, string En)> Sezioni = new[]
    {
        ("METAR & TAF", "METAR & TAF"),
        ("Dati generali", "General Data"),
        ("Radioassistenze", "Navaids"),
        ("Frequenze ATC/CRC", "ATC/CRC Freqs"),
        ("Aeroporti alternati", "Diversion Airfields"),
        ("Piste", "Runways"),
        ("Quote di transizione", "Transition Altitude/Level"),
        ("Nominativi", "Callsigns"),
        ("Procedure di terra", "Ground Procedures"),
        ("Parcheggi", "Parkings"),
        ("Messa in moto", "Engine Start"),
        ("Rullaggio", "Taxiing"),
        ("Armamento/disarmo", "Arming/Dearming"),
        ("Procedure di volo", "Flight Procedures"),
        ("Restrizioni al decollo", "Takeoff Restrictions"),
        ("Circuito SFO/precauzionale", "SFO / Precautional Circuit"),
        ("Avaria comunicazioni", "Commfail"),
        ("Circuito GCA", "GCA Circuit"),
        ("Porte e circuiti VFR jet", "VFR Jet Entry/Exit Gates and Circuits"),
        ("Punti significativi strumentali", "Instrumental Procedures Significant Points"),
        ("Partenze/arrivi IFR GAT", "IFR GAT Dep/Arr"),
        ("QRA / Scramble", "QRA / Scramble"),
        ("Aree di lavoro", "Working Areas"),
        ("Procedure generali", "General Procedures"),
        ("Bassa quota (BOAT)", "Low Level (BOAT)"),
        // Le carte dello scalo (3 settembre 2026): non vengono dai quindici PDF — sono una sezione nostra,
        // quindi l'inglese lo scriviamo noi, che è la ragione per cui questa tabella esiste. ⚠️ SID, STAR e
        // VFR sono SIGLE: si scrivono uguali nelle due lingue, e senza questa riga la macchina proverebbe a
        // tradurle.
        ("Carte aeroportuali", "Airport Charts"),
        ("Aerodromo", "Aerodrome"),
        ("Carte di avvicinamento strumentale", "Instrument Approach Charts"),
        ("SID", "SID"),
        ("STAR", "STAR"),
        ("VFR", "VFR"),
        ("Validità e revisione", "Validity and Revision"),
    };

    /// <summary>
    /// Le parole delle <b>tabelle</b> dei SOP: intestazioni di colonna e celle che si ripetono.
    ///
    /// <para>
    /// ⚠️ <b>Anche queste hanno un originale, e la macchina le sbagliava tutte.</b> Misurato sul primo SOP
    /// vero, il 28 agosto 2026: «Pista» → <i>Track</i>, «Piazzale» → <i>Forecourt</i>, «Stand» →
    /// <i>Booth</i>, «Rilevamento» → <i>Detection</i>, «Quota» → <i>Share</i>, «Ente» → <i>Institution</i>,
    /// «uscita/ingresso» → <i>Output/Input</i>. Nessuna è una sfumatura: sono le intestazioni delle colonne
    /// che un controllore legge per trovare il dato.
    /// </para>
    /// <para>
    /// ⚠️ Funziona perché la memoria è per <b>segmento intero</b>, e una cella di tabella <i>è</i> un
    /// segmento. Su una parola in mezzo a una frase non funzionerebbe — quella resta la parte aperta del
    /// glossario (<c>lavori-aperti §Q3</c>).
    /// </para>
    /// <para>
    /// ⚠️ <b>Queste voci valgono per TUTTI i documenti</b>, non solo per i militari: la memoria è una sola e
    /// non sa da quale documento venga il segmento. È voluto — «Rilevamento» è <i>Bearing</i> anche nella
    /// vIPI di un ACC — ma è la ragione per cui qui ci va solo ciò di cui si conosce l'originale, e non una
    /// resa che ci sembra migliore.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string It, string En)> Termini = new[]
    {
        // Intestazioni di colonna
        ("Tipo", "Type"),
        ("Nome", "Name"),
        ("Frequenza", "Frequency"),
        ("Coordinate", "Coordinates"),
        ("Ente", "Facility"),
        ("Nominativo", "Callsign"),
        ("Note", "Notes"),
        ("Aeroporto", "Airport"),
        ("Radioassistenza", "Navaid"),
        ("Rilevamento", "Bearing"),
        ("Distanza", "Distance"),
        ("Pista", "Runway"),
        ("Coordinate della soglia", "Threshold coordinates"),
        ("Reparto", "Squadron"),
        ("Nominativo OAT", "OAT callsign"),
        ("Nominativo GAT", "GAT callsign"),
        ("Piazzale", "Apron"),
        ("Stand", "Stand"),
        ("Usato da", "Used by"),
        ("Punto", "Point"),
        ("Riferimento", "Reference"),
        ("Quota", "Altitude"),
        ("Partenza", "Departure"),
        ("Arrivo", "Arrival"),

        // Celle che si ripetono
        ("transiti", "transit"),
        ("posizionamento autonomo", "self positioning"),
        ("posizione GCI", "GCI position"),
        ("Coordinate delle soglie.", "Threshold coordinates."),
    };

    /// <summary>
    /// Mette in memoria i titoli e i termini che non ci sono ancora. Idempotente e <b>non sovrascrive</b>: se qualcuno
    /// ha già corretto una voce a mano, la sua correzione vince su questa tabella.
    /// </summary>
    /// <returns>Quante voci ha scritto.</returns>
    public static async Task<int> SeminaAsync(ITranslationMemory memoria, CancellationToken ct = default)
    {
        // ⚠️ Le impronte UMANE, non tutte: una resa sbagliata già in memoria è di solito quella della
        // macchina, ed è proprio quella che va sostituita. Guardando «tutte» il seme non avrebbe corretto
        // niente su un sito che aveva già tradotto — cioè esattamente il caso in cui serve.
        var giaCi = await memoria.LoadHumanHashesAsync("it", "en", ct).ConfigureAwait(false);
        var scritte = 0;

        foreach (var (it, en) in Sezioni.Concat(Termini))
        {
            ct.ThrowIfCancellationRequested();
            if (giaCi.Contains(TranslationText.Hash(it))) continue;

            // reviewerUserId 0 = nessuna persona: è l'originale del documento di partenza, non la
            // correzione di qualcuno. La pagina di revisione lo mostra come già rivisto, ed è giusto.
            await memoria.SaveHumanAsync("it", "en", it, en, reviewerUserId: 0, ct).ConfigureAwait(false);
            scritte++;
        }

        return scritte;
    }
}
