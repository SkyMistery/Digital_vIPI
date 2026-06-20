using Vipi.Application.Aor;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Stato di resa di un blocco in vista.</summary>
public enum RenderState { Expanded, Collapsed }

/// <summary>Input minimo (DB-agnostico) per il calcolo di visibilità di un blocco.</summary>
public sealed record BlockInput(
    int BlockId,
    BlockVisibility Visibility,
    string? ScopeSectorKey,
    BlockTier Tier);

/// <summary>Esito di resa di un blocco: stato + etichetta di collasso morbido (PIANO §20.3).</summary>
public sealed record BlockRender(int BlockId, RenderState State, string? CollapseLabel);

/// <summary>
/// Applica la tabella di verità della visibilità (SPEC_Logica_AoR §4) producendo il modello di vista.
/// Collasso morbido: mai rimozione, sempre riespandibile. ADR-0001 D5.
/// </summary>
public interface IContentService
{
    /// <summary>
    /// Decide espanso/compresso per ogni blocco dati P, lo stato dei settori (AoR), il tier e la modalità live.
    /// In Live OFF: tutto espanso (consultazione completa).
    /// </summary>
    IReadOnlyList<BlockRender> BuildView(
        IEnumerable<BlockInput> blocks,
        AorResult aor,
        BlockTier tier,
        bool live);
}

/// <inheritdoc cref="IContentService"/>
public sealed class ContentService : IContentService
{
    public IReadOnlyList<BlockRender> BuildView(
        IEnumerable<BlockInput> blocks, AorResult aor, BlockTier tier, bool live)
    {
        var result = new List<BlockRender>();

        foreach (var b in blocks)
        {
            // Filtro tier: la Ridotta mostra solo i blocchi Reduced; l'Estesa mostra tutto.
            if (tier == BlockTier.Reduced && b.Tier != BlockTier.Reduced)
                continue;

            // Live OFF → tutto espanso, nessuna condizione AoR (SPEC §4, invariante S8).
            if (!live)
            {
                result.Add(new BlockRender(b.BlockId, RenderState.Expanded, null));
                continue;
            }

            // Always o blocco senza scope settore → sempre espanso.
            if (b.Visibility == BlockVisibility.Always || b.ScopeSectorKey is null)
            {
                result.Add(new BlockRender(b.BlockId, RenderState.Expanded, null));
                continue;
            }

            var state = aor.State.TryGetValue(b.ScopeSectorKey, out var s) ? s : SectorState.Covered;
            result.Add(Decide(b, state));
        }

        return result;
    }

    private static BlockRender Decide(BlockInput b, SectorState state) =>
        (b.Visibility, state) switch
        {
            // Copro io il settore → eseguo le procedure operative; non mi serve l'handoff verso me stesso.
            (BlockVisibility.Operational, SectorState.Covered) => Expand(b),
            (BlockVisibility.Operational, SectorState.Online)  => Collapse(b, $"{b.ScopeSectorKey} online — dettagli operativi delegati"),
            // Settore gestito da altri → serve la frequenza/coordinamento; le procedure operative non mi servono.
            (BlockVisibility.Handoff, SectorState.Online)      => Expand(b),
            (BlockVisibility.Handoff, SectorState.Covered)     => Collapse(b, $"Coordinamenti verso {b.ScopeSectorKey} — non necessari ora"),
            _ => Expand(b),
        };

    private static BlockRender Expand(BlockInput b) => new(b.BlockId, RenderState.Expanded, null);
    private static BlockRender Collapse(BlockInput b, string label) => new(b.BlockId, RenderState.Collapsed, label);
}
