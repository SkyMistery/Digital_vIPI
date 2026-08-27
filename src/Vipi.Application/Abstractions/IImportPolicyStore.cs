using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Stato immutabile della policy di import globale. <c>true</c> = la categoria è importata dalla sorgente
/// e bloccata (sola lettura); <c>false</c> = esclusa (manuale, l'import non la tocca).
/// </summary>
public sealed record ImportPolicySnapshot(
    bool TransitionAltitude, bool Runways, bool Sectors, bool Sids = true, bool SpecialAreas = true,
    bool AtcSessions = true)
{
    /// <summary>Tutto importato e bloccato: default opt-out.</summary>
    public static ImportPolicySnapshot AllImported { get; } = new(true, true, true, true, true, true);

    /// <summary>Vero se la categoria è importata dalla sorgente (quindi sola lettura per l'utente).</summary>
    public bool IsImported(ImportCategory category) => category switch
    {
        ImportCategory.TransitionAltitude => TransitionAltitude,
        ImportCategory.Runways => Runways,
        ImportCategory.Sectors => Sectors,
        ImportCategory.Sids => Sids,
        ImportCategory.SpecialAreas => SpecialAreas,
        ImportCategory.AtcSessions => AtcSessions,
        _ => true,
    };
}

/// <summary>
/// La policy più <b>chi</b> l'ha decisa e <b>quando</b>. Separata da <see cref="ImportPolicySnapshot"/> di
/// proposito: quello è lo stato che il dominio consulta a ogni import, e sta in cinque punti di
/// enforcement e in tre suite di test — aggiungergli l'autore lo trasformerebbe da «stato» in «stato + chi».
///
/// <para>⚠️ <paramref name="UpdatedByUserId"/> a <c>0</c> significa che <b>nessuna persona</b> ha mai salvato
/// questa policy: i valori che si vedono vengono dai default delle colonne. Non è un dettaglio da archivio —
/// <c>ImportSids</c> è nato <c>false</c> su un DB già popolato (migration <c>AddSidImport</c>, luglio 2026),
/// e senza questo campo un import fermo da mesi è indistinguibile da una scelta dell'amministratore.</para>
/// </summary>
/// <param name="RigaPresente">
/// Falso quando la tabella è <b>vuota</b>: la policy che si legge non è scritta da nessuna parte, viene dai
/// default del record. ⚠️ Distinguerlo da «riga c'è ma non l'ha decisa nessuno» serve alla diagnostica: nel
/// primo caso una <c>DELETE</c> ha riportato il regime a «la sorgente scrive tutto» e il primo giro dopo
/// sovrascrive TA e piste messe a mano; nel secondo i valori ci sono e uno può anche essere manuale.
/// </param>
public sealed record ImportPolicyInfo(ImportPolicySnapshot Policy, DateTime? UpdatedUtc, int UpdatedByUserId,
    bool RigaPresente = true)
{
    /// <summary>Vero se la riga non l'ha mai scritta una persona (o non c'è affatto).</summary>
    public bool MaiDecisa => UpdatedByUserId == 0;
}

/// <summary>Persistenza della policy di import globale (riga singola, get-or-create). Impl. EF.</summary>
public interface IImportPolicyStore
{
    /// <summary>Legge la policy corrente; crea la riga di default (tutto importato) se assente.</summary>
    Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>Come <see cref="GetAsync"/>, ma con autore e data dell'ultima decisione (pagina admin).</summary>
    Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default);

    /// <summary>Salva la policy, marcando autore e timestamp.</summary>
    Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default);
}
