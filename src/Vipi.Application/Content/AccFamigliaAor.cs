namespace Vipi.Application.Content;

/// <summary>
/// A quale AoR del blocco Aerovia appartiene un settore dell'ACC: quella principale, quella dei settori militari o
/// quella dei settori FSS.
/// </summary>
public enum FamigliaAor
{
    /// <summary>I settori di aerovia veri e propri: la mappa in cima al documento.</summary>
    Ordinaria,

    /// <summary>Il settore militare dell'ACC (<c>LIRR_MIL_CTR</c>): ha la sua sezione, prima delle aree regolamentate.</summary>
    Mil,

    /// <summary>I settori FSS (<c>LIRR_FSS</c>, <c>LIRR_PLN_FSS</c>): hanno la loro sezione, dopo le aree regolamentate.</summary>
    Fss,
}

/// <summary>
/// La regola che smista i settori del blocco Aerovia fra le tre AoR. Chiesta dal committente il 21 settembre 2026:
/// «ogni ACC ha almeno un settore FSS e un settore MIL, vorrei avessero due sezioni separate con la loro AoR e non
/// finissero nella AoR che c'è all'inizio».
///
/// <para><b>La regola sta nel callsign</b>, ed è quella detta dal committente e verificata sui dati:</para>
/// <list type="bullet">
///   <item><b>FSS</b>: l'ultimo pezzo è <c>FSS</c> — <c>LIRR_FSS</c>, <c>LIRR_PLN_FSS</c>, <c>LSAS_EXA_FSS</c>.</item>
///   <item><b>MIL</b>: un pezzo DI MEZZO contiene <c>MIL</c> — <c>LIRR_MIL_CTR</c>.</item>
/// </list>
///
/// <para>🔴 <b>Vale SOLO per il blocco Aerovia</b>, e chi la chiama deve saperlo. Nei blocchi APP ci sono
/// <c>LIEE_MIL_APP</c>, <c>LIEF_MIL_TWR</c>: la stessa regola li chiamerebbe «militari» e li toglierebbe dall'AoR
/// del loro blocco, dove invece stanno di diritto. È la ragione per cui questa classe non filtra niente da sé: dice
/// solo a che famiglia appartiene un callsign, e la decisione di applicarla la prende chi conosce il blocco.</para>
///
/// <para>⚠️ Perché non il <c>SectorType</c>: nella proiezione dei settori gli FSS hanno tipo <c>Ctr</c> (misurato
/// sul <c>vipi.db</c> il 21 settembre 2026: <c>LIRR_FSS</c>, <c>LIBB_FSS</c>… tutti <c>Ctr</c>), e un MIL è un CTR a
/// tutti gli effetti. Il tipo non li distingue; il nome sì.</para>
/// </summary>
public static class AccFamigliaAorRegola
{
    public static FamigliaAor Di(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return FamigliaAor.Ordinaria;
        var pezzi = callsign.Trim().ToUpperInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pezzi.Length < 2) return FamigliaAor.Ordinaria;

        if (pezzi[^1] == "FSS") return FamigliaAor.Fss;

        // Solo i pezzi DI MEZZO: il primo è l'ICAO dell'ente, l'ultimo il tipo di posizione.
        for (var i = 1; i < pezzi.Length - 1; i++)
            if (pezzi[i].Contains("MIL", StringComparison.Ordinal)) return FamigliaAor.Mil;

        return FamigliaAor.Ordinaria;
    }
}
