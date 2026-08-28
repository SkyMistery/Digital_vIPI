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
        ("Validità e revisione", "Validity and Revision"),
    };

    /// <summary>
    /// Mette in memoria i titoli che non ci sono ancora. Idempotente e <b>non sovrascrive</b>: se qualcuno
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

        foreach (var (it, en) in Sezioni)
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
