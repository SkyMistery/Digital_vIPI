using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il menu-sezioni degli editor elenca anche le SOTTO-sezioni (carta <c>2026-08-27-vsop-militari.md</c> §12, S1).
///
/// <para>⚠️ Fin qui elencava le sole radici. Su un documento a un livello solo è la stessa cosa; il vSOP
/// militare ha <b>venti sezioni su ventisei</b> annidate, e chi doveva scrivere le Radioassistenze scorreva
/// ventisei card per trovarle.</para>
///
/// <para><b>Perché una funzione pura e non un render dell'editor.</b> <c>DocumentSectionsEditor</c> è un
/// <c>OwningComponentBase</c> con servizio di editing, lock e JS: montarlo per contare le voci di un menu
/// costerebbe una fixture, e proverebbe soprattutto la fixture. La proiezione è la parte che si può
/// sbagliare in silenzio — è lo stesso motivo per cui <c>SectionOrdering.TryDropOnto</c> vive da sola.</para>
/// </summary>
public class EditorTocProjectionTests
{
    private static EditableSection Sez(int id, string titolo, int depth = 0, params EditableSection[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        SectionKey = titolo.ToLowerInvariant(),
        Depth = depth,
        Order = id,
        Blocks = Array.Empty<EditableBlock>(),
        Children = figlie,
    };

    /// <summary>Come <see cref="Sez"/>, ma con la CHIAVE di catalogo vera: quella che decide il titolo.</summary>
    private static EditableSection ConChiave(int id, string titolo, string chiave, int depth,
                                             params EditableSection[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        SectionKey = chiave,
        Depth = depth,
        Order = id,
        Blocks = Array.Empty<EditableBlock>(),
        Children = figlie,
    };

    private static IReadOnlyList<EditorTocItem> Voci(params EditableSection[] radici) =>
        EditorTocProjection.DaSezioni(radici, dirty: null, dragGroup: "root");

    [Fact]
    public void Le_figlie_seguono_la_loro_radice_nell_ordine_del_documento()
    {
        var voci = Voci(
            Sez(1, "Dati generali", 0, Sez(2, "Radioassistenze", 1), Sez(3, "Frequenze ATC/CRC", 1)),
            Sez(4, "Procedure di terra", 0, Sez(5, "Parcheggi", 1)));

        Assert.Equal(
            new[] { "Dati generali", "Radioassistenze", "Frequenze ATC/CRC", "Procedure di terra", "Parcheggi" },
            voci.Select(v => v.Label));
        Assert.Equal(new[] { "s-1", "s-2", "s-3", "s-4", "s-5" }, voci.Select(v => v.AnchorId));
    }

    /// <summary>Le figlie rientrano; le radici restano al livello di prima — nessun documento cambia aspetto
    /// per il fatto che ora si scende.</summary>
    [Fact]
    public void Le_figlie_rientrano_e_le_radici_restano_dov_erano()
    {
        var voci = Voci(Sez(1, "Dati generali", 0, Sez(2, "Radioassistenze", 1)));

        Assert.Equal(2, voci[0].Level);
        Assert.Equal(3, voci[1].Level);
    }

    /// <summary>
    /// ⚠️ Una figlia <b>non si trascina</b>: il riordino lavora per gruppo di fratelli, e aprirlo ai figli è
    /// un lavoro suo. Una voce che si lascia prendere e poi non va da nessuna parte è il difetto già pagato
    /// con la voce del pannello Release.
    /// </summary>
    [Fact]
    public void Una_figlia_non_si_trascina_e_una_radice_si()
    {
        var voci = Voci(Sez(1, "Dati generali", 0, Sez(2, "Radioassistenze", 1)));

        Assert.Equal(1, voci[0].SectionId);
        Assert.Equal("root", voci[0].DragGroup);
        Assert.Null(voci[1].SectionId);
        Assert.Null(voci[1].DragGroup);
    }

    /// <summary>Il pallino delle modifiche non salvate vale anche per le figlie: è lì che si sta scrivendo.</summary>
    [Fact]
    public void Il_pallino_delle_modifiche_vale_anche_per_le_figlie()
    {
        var radice = Sez(1, "Dati generali", 0, Sez(2, "Radioassistenze", 1));

        var voci = EditorTocProjection.DaSezioni(new[] { radice }, s => s.Id == 2, "root");

        Assert.False(voci[0].Dirty);
        Assert.True(voci[1].Dirty);
    }

    /// <summary>Il modello consente tre livelli, l'indice ne disegna due: più giù non si rientra oltre, o in
    /// una colonna da 200px il titolo finirebbe fuori.</summary>
    [Fact]
    public void Sotto_il_terzo_livello_il_rientro_non_cresce_piu()
    {
        var voci = Voci(Sez(1, "A", 0, Sez(2, "B", 1, Sez(3, "C", 2))));

        Assert.Equal(new[] { 2, 3, 3 }, voci.Select(v => v.Level));
    }

    /// <summary>Un documento senza annidamenti — le altre quattro famiglie — resta identico a prima.</summary>
    [Fact]
    public void Un_documento_piatto_non_cambia()
    {
        var voci = Voci(Sez(1, "Separazioni"), Sez(2, "AOR"));

        Assert.Equal(2, voci.Count);
        Assert.All(voci, v => Assert.Equal(2, v.Level));
        Assert.All(voci, v => Assert.NotNull(v.SectionId));
    }

    /// <summary>
    /// L'indice chiama le sezioni come le chiamano le card: <b>a fondo</b>, non con quel che il documento si
    /// porta dietro.
    ///
    /// <para>⚠️ Il titolo di una sezione di catalogo sta scritto nel documento nella lingua che aveva alla
    /// NASCITA e nessuno lo aggiorna quando la lingua cambia; le card lo risolvono dal catalogo
    /// (<c>DocumentSectionsEditor.Titolo</c>), e senza lo stesso passo qui l'indice direbbe «Dati generali»
    /// accanto a una card intitolata «General data» — sulla stessa schermata, e nessun test lo vedrebbe.</para>
    /// </summary>
    [Fact]
    public void L_indice_usa_il_titolo_che_la_pagina_mostra()
    {
        var figlia = ConChiave(2, "Radioassistenze", "navaids", depth: 1);
        var radice = ConChiave(1, "Dati generali", "generaldata", depth: 0, figlia);

        var voci = EditorTocProjection.DaSezioni(
            new[] { radice }, dirty: null, dragGroup: "root",
            titolo: s => TitoliDiCatalogo.Titolo(SectionProfile.AirportMil, s.SectionKey, s.Title, "en"));

        // ⚠️ Anche la FIGLIA: il vSOP militare ha venti sezioni su ventisei annidate, ed è lì che il difetto
        // si vede.
        Assert.Equal("General data", voci[0].Label);
        Assert.Equal("Navigation aids", voci[1].Label);
    }
}
