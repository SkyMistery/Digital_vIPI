using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Da dove un <b>documento</b> prende le configurazioni dei suoi gruppi: dalla Struttura, non più da sé.
/// Carta <c>docs/feature/2026-10-08-configurazioni-possibili.md</c> §4–§5.
///
/// <para>La regola è una, e la seguono la vIPI dell'ACC e la vIPI APP:</para>
/// <list type="bullet">
/// <item><b>versione di lavoro</b> (editor, anteprima di una bozza, cattura alla pubblicazione): la struttura
/// di adesso;</item>
/// <item><b>release uscita da quando le configurazioni stanno in struttura</b>
/// (<see cref="DocReleasePayload.ConfigurazioniDallaStruttura"/>): la voce congelata della sezione, se la
/// sezione è Frozen; altrimenti (sezione Live) la struttura di adesso;</item>
/// <item><b>release di prima</b>: il <c>BodyJson</c> della sezione <c>configurations</c> dentro lo snapshot,
/// com'è. Una release già uscita non cambia perché è cambiato il posto dove si scrive.</item>
/// </list>
/// </summary>
internal static class ConfigurazioniDelDocumento
{
    public const string Chiave = "configurations";

    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Il gruppo di un blocco della vIPI ACC: i settori d'area dell'ACC per l'Aerovia, l'ente del gruppo per un
    /// gruppo APP (<see cref="AtcUnitRow.GroupKey"/> è la chiave del blocco; <see cref="AccBlock.UnitId"/> per i
    /// gruppi nati con «Remotizza» prima che la chiave ci fosse). <c>null</c> = un gruppo senza ente (nessun
    /// membro): non ha configurazioni.
    /// </summary>
    public static (ConfigurationGroupKind Genere, string Codice)? GruppoDi(
        string accCode, AccBlock blocco, IReadOnlyList<AtcUnitRow> enti)
    {
        if (blocco.Kind == AccBlockKind.Aerovia) return (ConfigurationGroupKind.AccArea, accCode);
        var ente = enti.FirstOrDefault(u => OIC.Equals(u.GroupKey, blocco.Key))
                   ?? (blocco.UnitId is int id ? enti.FirstOrDefault(u => u.Id == id) : null);
        return ente is null ? null : (ConfigurationGroupKind.AtcUnit, ente.Code);
    }

    /// <summary>Mette in ogni blocco l'elenco che la Struttura ha <b>adesso</b> per il suo gruppo.</summary>
    public static async Task DallaStrutturaAsync(
        ISectorConfigurationService configurazioni, IAtcUnitRepository? enti,
        string accCode, IEnumerable<AccBlock> blocchi, CancellationToken ct)
    {
        IReadOnlyList<AtcUnitRow>? deiGruppi = null;
        foreach (var b in blocchi)
        {
            if (b.Kind != AccBlockKind.Aerovia)
                deiGruppi ??= enti is null ? Array.Empty<AtcUnitRow>() : await enti.ListAsync(accCode, ct);
            b.Configurations = GruppoDi(accCode, b, deiGruppi ?? Array.Empty<AtcUnitRow>()) is { } g
                ? (await configurazioni.ListAsync(g.Genere, g.Codice, ct)).ToList()
                : new List<AccConfiguration>();
        }
    }

    /// <summary>
    /// Le configurazioni dei blocchi di una <b>release</b>, secondo la regola qui sopra. I blocchi arrivano
    /// assemblati dallo snapshot: portano già il <c>BodyJson</c> di allora, che resta per le release di prima.
    /// </summary>
    public static async Task DellaReleaseAsync(
        ISectorConfigurationService configurazioni, IAtcUnitRepository? enti,
        string accCode, IReadOnlyList<AccAssembledBlock> blocchi, DocReleasePayload payload, CancellationToken ct)
    {
        if (!payload.ConfigurazioniDallaStruttura) return;   // release di prima: lo snapshot com'è

        var vive = new List<AccBlock>();
        foreach (var ab in blocchi)
        {
            if (ab.ChildSectionIdsByKey.TryGetValue(Chiave, out var sid)
                && payload.FrozenSections.TryGetValue(sid, out var congelata))
                ab.Block.Configurations = ConfigurazioniJson.Leggi(congelata);
            else
                vive.Add(ab.Block);   // sezione Live (o assente): la struttura di adesso
        }
        if (vive.Count > 0) await DallaStrutturaAsync(configurazioni, enti, accCode, vive, ct);
    }
}
