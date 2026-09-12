namespace Vipi.Application.Content;

/// <summary>
/// I minimi LVP di uno scalo, in forma editoriale. <c>Id == 0</c> = riga nuova.
/// <para>Ogni soglia può mancare: uno scalo che dichiara solo l'RVR non deve inventarsi un soffitto.</para>
/// </summary>
public sealed record LvpRow(int Id, bool Declared,
    int? PrepRvrM, int? PrepCeilingFt,
    int? LvpRvrM, int? LvpCeilingFt,
    int? CancelRvrM, int? CancelCeilingFt,
    string? Note)
{
    /// <summary>Vero se non c'è nessuna soglia scritta: la riga esiste ma non dice niente.</summary>
    public bool NessunaSoglia =>
        PrepRvrM is null && PrepCeilingFt is null && LvpRvrM is null && LvpCeilingFt is null
        && CancelRvrM is null && CancelCeilingFt is null;
}

/// <summary>
/// I minimi <b>standard</b>, quelli che l'editor propone quando uno scalo non ne ha di suoi e su cui il quadro
/// vAWOS ricade dichiarandolo.
///
/// <para>⚠️ <b>Stanno in un posto solo</b>: li citano l'editor (che precompila) e il quadro (che ricade). Se un
/// giorno si scoprisse che uno scalo italiano usa una soglia diversa, si cambia qui — e non in due posti che
/// col tempo direbbero due cose.</para>
///
/// <para>⚠️ <b>Sono un default, non un fatto.</b> Non si scrivono d'ufficio nell'anagrafica di nessuno scalo:
/// seminarli su settanta aeroporti vorrebbe dire affermare, per settanta volte, una cosa che nessuno ha
/// letto sull'AIP. L'editor li propone e qualcuno li salva; finché non lo fa, in archivio non c'è niente e il
/// quadro scrive «standard» accanto allo stato.</para>
/// </summary>
public static class LvpStandard
{
    /// <summary>Fase preparatoria: RVR ≤ 800 m oppure soffitto ≤ 300 ft.</summary>
    public const int PrepRvrM = 800;
    public const int PrepCeilingFt = 300;

    /// <summary>LVP in vigore: RVR &lt; 550 m oppure soffitto &lt; 200 ft.</summary>
    public const int LvpRvrM = 550;
    public const int LvpCeilingFt = 200;

    /// <summary>Cancellazione: RVR &gt; 800 m e soffitto &gt; 300 ft, con tendenza al miglioramento.</summary>
    public const int CancelRvrM = 800;
    public const int CancelCeilingFt = 300;

    /// <summary>La riga standard, per precompilare l'editor o per il ripiego del quadro.</summary>
    public static LvpRow Riga => new(0, true, PrepRvrM, PrepCeilingFt, LvpRvrM, LvpCeilingFt,
                                     CancelRvrM, CancelCeilingFt, null);
}

/// <summary>Lo stato LVP suggerito dal dato meteo corrente.</summary>
public enum LvpStato
{
    /// <summary>Sopra tutte le soglie: niente da fare.</summary>
    Nil,
    /// <summary>Sotto le soglie della fase preparatoria.</summary>
    Preparazione,
    /// <summary>Sotto le soglie di LVP in vigore.</summary>
    InVigore,
    /// <summary>Lo scalo dichiara di non operare in LVP.</summary>
    NonApplicabile,
    /// <summary>Non ci sono minimi né soglie da confrontare, o manca il dato meteo.</summary>
    NonValutabile,
}

/// <summary>Da dove viene la misura di visibilità con cui si è valutato.</summary>
public enum LvpMisura
{
    /// <summary>Un gruppo RVR del METAR: è la misura giusta.</summary>
    Rvr,
    /// <summary>La visibilità prevalente, in mancanza di RVR. ⚠️ <b>Non</b> è la stessa cosa, e si dichiara.</summary>
    Visibilita,
    /// <summary>Nessuna delle due.</summary>
    Nessuna,
}

/// <summary>
/// L'esito della valutazione LVP: lo stato, da che cosa è stato dedotto, e con quali minimi.
/// </summary>
/// <param name="DaiMinimiDelloScalo">
/// Falso = si sono usati i minimi <see cref="LvpStandard"/>, perché lo scalo non ne ha di suoi. Il quadro lo
/// scrive: una soglia standard è utile, una soglia standard spacciata per quella di Fiumicino no.
/// </param>
public sealed record LvpValutazione(LvpStato Stato, LvpMisura Misura, bool DaiMinimiDelloScalo,
                                    int? RvrM, int? CeilingFt)
{
    public static readonly LvpValutazione NonValutabile =
        new(LvpStato.NonValutabile, LvpMisura.Nessuna, false, null, null);
}

/// <summary>
/// Confronta il dato meteo coi minimi dello scalo e dice <b>che cosa suggerisce</b>.
///
/// <para>🔴 <b>Suggerisce, non decide.</b> L'attivazione delle LVP è una procedura d'aeroporto, con dentro
/// cose che il METAR non sa (lo stato dei sensori, i lavori sul piazzale, la decisione di chi comanda). Un
/// quadro che la dichiarasse da solo direbbe una cosa che nessun controllore ha deciso, e chi legge non
/// avrebbe modo di accorgersene.</para>
/// </summary>
public static class LvpValutatore
{
    /// <param name="minimi">I minimi dello scalo; <c>null</c> = non dichiarati, si ricade sullo standard.</param>
    /// <param name="rvrM">Il <b>minimo</b> fra i gruppi RVR del bollettino, se ce ne sono.</param>
    /// <param name="visibilitaM">La visibilità prevalente, in metri, usata solo se l'RVR non c'è.</param>
    /// <param name="ceilingFt">Il soffitto (BKN/OVC più basso, o la visibilità verticale).</param>
    public static LvpValutazione Valuta(LvpRow? minimi, int? rvrM, int? visibilitaM, int? ceilingFt)
    {
        var dalloScalo = minimi is not null;
        var m = minimi ?? LvpStandard.Riga;

        if (!m.Declared)
            return new LvpValutazione(LvpStato.NonApplicabile, LvpMisura.Nessuna, dalloScalo, null, null);
        if (m.NessunaSoglia)
            return LvpValutazione.NonValutabile with { DaiMinimiDelloScalo = dalloScalo };

        // ⚠️ L'RVR batte la visibilità e non è un ripiego equivalente: sono due misure diverse — una guarda
        // lungo la pista con le luci accese, l'altra guarda in giro — e quando si usa la seconda va detto.
        var misura = rvrM is not null ? LvpMisura.Rvr
                   : visibilitaM is not null ? LvpMisura.Visibilita
                   : LvpMisura.Nessuna;
        var vis = rvrM ?? visibilitaM;

        if (vis is null && ceilingFt is null)
            return LvpValutazione.NonValutabile with { DaiMinimiDelloScalo = dalloScalo };

        // Sotto una QUALUNQUE delle due soglie: basta una a far scattare la fase, e l'altra che manca non
        // la trattiene. È la forma in cui le procedure sono scritte («RVR ... or ceiling ...»).
        if (Sotto(vis, m.LvpRvrM) || Sotto(ceilingFt, m.LvpCeilingFt))
            return new LvpValutazione(LvpStato.InVigore, misura, dalloScalo, vis, ceilingFt);

        if (SottoOUguale(vis, m.PrepRvrM) || SottoOUguale(ceilingFt, m.PrepCeilingFt))
            return new LvpValutazione(LvpStato.Preparazione, misura, dalloScalo, vis, ceilingFt);

        return new LvpValutazione(LvpStato.Nil, misura, dalloScalo, vis, ceilingFt);
    }

    // Una soglia che lo scalo non ha scritto non si valuta: non è «zero», è «non pertinente».
    private static bool Sotto(int? valore, int? soglia) => valore is int v && soglia is int s && v < s;
    private static bool SottoOUguale(int? valore, int? soglia) => valore is int v && soglia is int s && v <= s;
}
