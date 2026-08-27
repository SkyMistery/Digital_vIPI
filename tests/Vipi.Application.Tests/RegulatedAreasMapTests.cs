using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le aree regolamentate viste come una mappa sola (27 agosto 2026): colore per tipo, banda FL per il 3D,
/// preset per tipo.
///
/// <para><b>Perché serve.</b> Questa traduzione è l'unico punto in cui un'area diventa un «settore» della
/// mappa dell'AoR, e ci sono due scambi facili da fare e impossibili da vedere a schermo: usare il NOME
/// invece dell'id come chiave (i nomi hanno spazi e finiscono in un selettore CSS), e passare i piedi dove
/// il visore 3D si aspetta un FL — un'area a 29 000 ft diventerebbe un prisma alto FL 29 000.</para>
/// </summary>
public class RegulatedAreasMapTests
{
    private static AccSpecialAreaView Area(string id, string nome, string? tipo, int? min = 0, int? max = 5000,
        bool shape = true) =>
        new(id, nome, tipo, "descrizione", "Permanently active", min, max, shape ? Poly() : null);

    private static AppAorPolygon Poly() =>
        new("0 0 10 10", "M0 0 L10 0 L10 10 Z", new List<double[]> { new[] { 41.0, 12.0 }, new[] { 41.5, 12.5 }, new[] { 41.0, 12.5 } },
            41.0, 12.0, 41.5, 12.5, 41.25, 12.25);

    // ⚠️ La chiave è l'id IVAO, non il nome: il nome finisce in un selettore [data-sec="…"] e contiene
    // spazi e punti. Il nome sta nell'etichetta, che è testo e non deve essere valida come selettore.
    [Fact]
    public void La_chiave_e_lid_ivao_il_nome_sta_nelletichetta()
    {
        var v = RegulatedAreasMap.Build(new[] { Area("1113", "LI R300A Amendola", "R") });

        var s = Assert.Single(v.Sectors);
        Assert.Equal("1113", s.Callsign);
        Assert.Equal("LI R300A Amendola", s.Name);
        Assert.Equal("R300A Amendola", s.Label);   // il prefisso «LI » ce l'hanno quasi tutte: non distingue
    }

    [Fact]
    public void Letichetta_lascia_stare_i_nomi_che_non_cominciano_per_LI()
    {
        var v = RegulatedAreasMap.Build(new[] { Area("735", "INDIA6", "R"), Area("9", "LI", "R") });

        Assert.Equal("INDIA6", v.Sectors[0].Label);
        Assert.Equal("LI", v.Sectors[1].Label);    // «LI» soltanto: non è un prefisso, è tutto il nome
    }

    [Fact]
    public void Il_colore_viene_dal_tipo()
    {
        var v = RegulatedAreasMap.Build(new[]
        {
            Area("1", "LI R1", "R"), Area("2", "LI D1", "D"), Area("3", "LI P1", "P"),
            Area("4", "TSA 1", "TSA"), Area("5", "TRA 1", "TRA"), Area("6", "Boh", null),
        });

        Assert.Equal(SpecialAreaColorScheme.Defaults["R"], v.Sectors[0].Color);
        Assert.Equal(SpecialAreaColorScheme.Defaults["D"], v.Sectors[1].Color);
        Assert.Equal(SpecialAreaColorScheme.Defaults["P"], v.Sectors[2].Color);
        Assert.Equal(SpecialAreaColorScheme.Defaults["TSA"], v.Sectors[3].Color);
        Assert.Equal(SpecialAreaColorScheme.Defaults["TRA"], v.Sectors[4].Color);
        Assert.Equal(SpecialAreaColorScheme.Fallback, v.Sectors[5].Color);
        // Cinque tipi, cinque colori diversi: se due combaciassero la mappa non li distinguerebbe.
        Assert.Equal(5, SpecialAreaColorScheme.Defaults.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // Le quote delle aree sono in PIEDI (29000, 1500). Il visore 3D estrude su una banda FL: senza la
    // conversione un'area a 29 000 ft diventa un prisma alto FL 29 000.
    [Fact]
    public void Le_quote_in_piedi_diventano_una_banda_FL()
    {
        var v = RegulatedAreasMap.Build(new[]
        {
            Area("1", "alta", "R", 14500, 24500),
            Area("2", "dal suolo", "R", 0, 4000),
            Area("3", "senza limiti", "R", null, null),
        });

        Assert.Equal(145, v.Sectors[0].LowerFl);
        Assert.Equal(245, v.Sectors[0].UpperFl);
        Assert.Equal(AorFlBand.Ground, v.Sectors[1].LowerFl);
        Assert.Equal(40, v.Sectors[1].UpperFl);
        Assert.Equal(AorFlBand.Ground, v.Sectors[2].LowerFl);
        Assert.Equal(AorFlBand.Unlimited, v.Sectors[2].UpperFl);
    }

    // Un'area senza shape non si disegna, ma la sua descrizione resta l'unica cosa che di lei si può dire:
    // se sparisse dall'elenco dei «settori» sparirebbe anche la chip che accende quella descrizione.
    [Fact]
    public void Unarea_senza_shape_resta_nellelenco_senza_poligoni()
    {
        var v = RegulatedAreasMap.Build(new[] { Area("1", "con", "R"), Area("2", "senza", "R", shape: false) });

        Assert.Equal(2, v.Sectors.Count);
        Assert.Single(v.Sectors[0].Polygons);
        Assert.Empty(v.Sectors[1].Polygons);
    }

    [Fact]
    public void Nessuna_area_nessuna_vista()
    {
        Assert.Same(AccAorView.Empty, RegulatedAreasMap.Build(Array.Empty<AccSpecialAreaView>()));
        Assert.Empty(RegulatedAreasMap.Presets(Array.Empty<AccSpecialAreaView>()));
    }

    // I preset riusano il contratto delle chip-configurazione dell'AoR: «accendi esattamente questo insieme».
    [Fact]
    public void I_preset_raggruppano_per_tipo_nellordine_del_catalogo()
    {
        var aree = new[]
        {
            Area("1", "TRA uno", "TRA"), Area("2", "LI D1", "D"), Area("3", "LI R1", "R"),
            Area("4", "LI R2", "r"),      // stesso tipo scritto minuscolo: NON è un sesto gruppo
            Area("5", "Ignota", "ZZZ"),
        };

        var p = RegulatedAreasMap.Presets(aree);

        // Ordine stabile: prima i tipi noti nell'ordine del catalogo (R, D, P, TSA, TRA), poi gli ignoti.
        Assert.Equal(new[] { "R", "D", "TRA", "ZZZ" }, p.Select(x => x.Key));
        Assert.Equal(new[] { "3", "4" }, p.First(x => x.Key == "R").OpenCallsigns);
        // Ogni area sta in UN preset solo, e tutte ci stanno: nessuna resta irraggiungibile dai tasti.
        Assert.Equal(aree.Length, p.Sum(x => x.OpenCallsigns.Count));
    }

    // ⚠️ I preset devono stare DENTRO la vista, non accanto: la fila di tasti la disegna AccAor leggendo
    // `Configs`, e costruirli senza metterceli è esattamente come non averli — alla prima prova dal vivo
    // la fila non compariva. Il test guarda la vista, non la funzione che li fabbrica.
    [Fact]
    public void I_preset_viaggiano_dentro_la_vista()
    {
        var v = RegulatedAreasMap.Build(new[] { Area("1", "LI R1", "R"), Area("2", "LI D1", "D") });

        Assert.Equal(new[] { "R", "D" }, v.Configs.Select(x => x.Name));
    }
}
