using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Materializza la struttura OBBLIGATORIA della vLOA (<see cref="VloaSections"/>) in <see cref="DocumentSection"/>/
/// <see cref="ContentBlock"/> su una versione. Usato alla generazione della vLOA da «ACC confinanti».
/// Imposta <c>RowVersion</c> come l'editor (<c>AddSectionAsync</c>/<c>AddBlockAsync</c>) per la concorrenza ottimistica.
/// </summary>
public static class VloaStructureSeeder
{
    public static void Seed(VipiDbContext db, DocumentVersion ver, IReadOnlyList<VloaSectionSpec> specs)
    {
        var order = 1;
        foreach (var spec in specs)
            AddSection(db, ver, spec, parent: null, order++);
    }

    private static void AddSection(VipiDbContext db, DocumentVersion ver, VloaSectionSpec spec, DocumentSection? parent, int order)
    {
        var section = new DocumentSection
        {
            DocumentVersion = ver,
            ParentSection = parent,
            Title = spec.Title,
            Order = order,
            Depth = parent is null ? 0 : parent.Depth + 1,
            SectionKind = spec.Kind,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        ver.Sections.Add(section);
        db.DocumentSections.Add(section);

        var blockOrder = 1;
        foreach (var b in spec.Blocks)
        {
            var block = new ContentBlock
            {
                DocumentVersion = ver,
                Section = section,
                Order = blockOrder++,
                Tier = Domain.BlockTier.Reduced,
                Format = b.Format,
                Visibility = Domain.BlockVisibility.Always,
                CalloutKind = b.CalloutKind,
                Body = b.Body,
                BodyJson = b.BodyJson,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            section.Blocks.Add(block);
            ver.Blocks.Add(block);
            db.ContentBlocks.Add(block);
        }

        var childOrder = 1;
        foreach (var child in spec.Children)
            AddSection(db, ver, child, section, childOrder++);
    }
}
