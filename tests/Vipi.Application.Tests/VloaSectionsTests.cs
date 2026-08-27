using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La struttura della vLOA la dice il catalogo, i contenuti li dice <see cref="VloaSections"/> (doc 13 §3c).
/// Prima erano due descrizioni parallele e divergenti; queste prove impediscono che tornino a separarsi.
/// </summary>
public class VloaSectionsTests
{
    private static IReadOnlyList<VloaSectionSpec> Canonical() =>
        VloaSections.Canonical("LIBB", "LDZO", "Zagreb");

    [Fact]
    public void Structure_comes_from_the_catalog()
    {
        var expected = SectionCatalog.For(SectionProfile.Vloa).OrderBy(d => d.Order).ToList();
        var actual = Canonical();

        Assert.Equal(expected.Select(d => d.Key), actual.Select(s => s.SectionKey));
        Assert.Equal(expected.Select(d => d.Title), actual.Select(s => s.Title));
    }

    [Fact]
    public void Every_section_is_recognised_as_mandatory_by_the_catalog()
    {
        // È la stessa domanda che si fa l'editor: se una sezione seminata non fosse «fissa», nascerebbe
        // rinominabile ed eliminabile — che è il difetto che l'identificazione per titolo mascherava.
        foreach (var s in Canonical())
            Assert.True(SectionCatalog.IsFixed(SectionProfile.Vloa, s.SectionKey), s.SectionKey);
    }

    [Fact]
    public void Coordination_has_the_two_directions_and_no_body_of_its_own()
    {
        var coord = Canonical().Single(s => s.SectionKey == "coordination");

        Assert.Empty(coord.Blocks);   // il corpo lo produce l'editor/viewer, non i blocchi
        Assert.Equal(2, coord.Children.Count);
        // Ogni direzione ha la SUA chiave: prima ripetevano quella del padre, e la cattura frozen trovava tre
        // sezioni «coordination» derivando tre volte lo stesso payload.
        Assert.Equal(SectionKeys.CoordinationOut, coord.Children[0].SectionKey);
        Assert.Equal("LIBB → LDZO", coord.Children[0].Title);
        Assert.Equal(SectionKeys.CoordinationIn, coord.Children[1].SectionKey);
        Assert.Equal("LDZO → LIBB", coord.Children[1].Title);
    }

    [Fact]
    public void The_two_directions_carry_no_blocks()
    {
        // Prima ciascuna nasceva con un paragrafo che NESSUNA vista rendeva: l'editor le tratta come derivate
        // (tabella al posto dei blocchi) e il viewer rende le direzioni dal padre. Contenuto scritto nel DB di
        // ogni vLOA e invisibile ovunque.
        var coord = Canonical().Single(s => s.SectionKey == "coordination");
        Assert.All(coord.Children, c => Assert.Empty(c.Blocks));
    }

    [Fact]
    public void The_directions_are_fixed_and_host_rendered_but_carry_no_toggle()
    {
        foreach (var key in new[] { SectionKeys.CoordinationOut, SectionKeys.CoordinationIn })
        {
            Assert.True(SectionCatalog.IsFixed(SectionProfile.Vloa, key));          // non rinominabili né eliminabili
            Assert.True(SectionCatalog.IsHostRendered(SectionProfile.Vloa, key));   // corpo = tabella dei trasferimenti
            Assert.False(SectionCatalog.IsRenderModeToggleable(key));               // il congelamento è del padre
        }
    }

    /// <summary>
    /// ⚠️ Questo test asseriva il DIFETTO: pretendeva che la tabella di «validity» contenesse il ciclo AIRAC
    /// passato alla creazione («2609»). Era la forma di prova più insidiosa — verde, precisa, e a guardia della
    /// cosa sbagliata. Il ciclo lo dice la scheda della release; quello che qui si semina è solo il testo che
    /// nessuno può derivare. Vedi doc 14 §3b e <see cref="La_validita_non_scrive_il_ciclo_AIRAC"/>.
    /// </summary>
    [Fact]
    public void Validity_carries_the_starting_table_and_purpose_the_intro()
    {
        var validity = Canonical().Single(s => s.SectionKey == "validity");
        var block = Assert.Single(validity.Blocks);
        Assert.Equal(BlockFormat.Table, block.Format);
        Assert.Contains("Review cycle", block.BodyJson);        // concordato fra le due parti: non derivabile
        Assert.Contains("LIBB CH / AOD", block.BodyJson);       // il firmatario: nemmeno lui

        var purpose = Canonical().Single(s => s.SectionKey == "purpose");
        Assert.Contains("LIBB", Assert.Single(purpose.Blocks).Body);
    }

    /// <summary>
    /// doc 14 §3b — il contenuto iniziale non scrive più il ciclo AIRAC. Lo dice la scheda della release, che si
    /// aggiorna da sé; una tabella che lo ripete congela il ciclo del giorno della creazione e comincia a mentire
    /// alla prima ripubblicazione. Restano le due cose che nessuno può derivare.
    /// </summary>
    [Fact]
    public void La_validita_non_scrive_il_ciclo_AIRAC()
    {
        var validity = Canonical().Single(s => s.SectionKey == "validity");
        var tabella = Assert.Single(validity.Blocks);
        Assert.Equal(BlockFormat.Table, tabella.Format);

        Assert.DoesNotContain("AIRAC", tabella.BodyJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Effective from", tabella.BodyJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Review cycle", tabella.BodyJson);
        Assert.Contains("Italian signatory", tabella.BodyJson);
    }

    /// <summary>Rete più larga della precedente: nessun blocco seminato, di nessuna sezione, scrive un ciclo
    /// AIRAC. Se qualcuno ne aggiungesse uno altrove, questo test se ne accorgerebbe.</summary>
    [Fact]
    public void Nessun_blocco_seminato_scrive_un_ciclo_AIRAC()
    {
        foreach (var sezione in Canonical())
            foreach (var b in sezione.Blocks)
            {
                Assert.DoesNotContain("AIRAC", b.Body ?? "", StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("AIRAC", b.BodyJson ?? "", StringComparison.OrdinalIgnoreCase);
            }
    }
}
