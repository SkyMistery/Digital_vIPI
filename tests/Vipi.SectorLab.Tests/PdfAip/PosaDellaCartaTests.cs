using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>Carta «Gli aeroporti nel Lab», S3: l'immagine della pagina (PDFium, PNG) e la posa della carta sulla mappa.</summary>
public sealed class PosaDellaCartaTests
{
    /// <summary>Una pagina A5 bianca con un quadrato nero pieno a 100 pt da sinistra e 50 dall'alto, lato 20.</summary>
    private static byte[] PaginaColQuadrato()
    {
        var builder = new PdfDocumentBuilder();
        var pagina = builder.AddPage(420, 595);
        pagina.SetTextAndFillColor(0, 0, 0);
        pagina.DrawRectangle(new PdfPoint(100, 595 - 50 - 20), 20, 20, 1, fill: true);
        builder.AddPage(420, 595);
        return builder.Build();
    }

    [Fact]
    public void Pdfium_disegna_la_pagina_con_la_sua_misura_su_fondo_bianco()
    {
        var immagine = Pdfium.Disegna(PaginaColQuadrato(), 1, dpi: 72);

        Assert.Equal(420, immagine.Larghezza);
        Assert.Equal(595, immagine.Altezza);
        Assert.Equal(420, immagine.LarghezzaInPunti, 3);
        Assert.Equal(595, immagine.AltezzaInPunti, 3);
        byte Rosso(int x, int y) => immagine.Rgb[(y * immagine.Larghezza + x) * 3];
        Assert.Equal(255, Rosso(10, 10));       // fondo bianco, non trasparente
        Assert.True(Rosso(110, 60) < 40);       // dentro il quadrato: la y della pagina è dall'alto
        Assert.Equal(255, Rosso(110, 90));      // sotto il quadrato
    }

    [Fact]
    public void Pdfium_dice_perche_se_la_pagina_non_ce_o_il_pdf_non_si_apre()
    {
        var oltre = Assert.Throws<InvalidDataException>(() => Pdfium.Disegna(PaginaColQuadrato(), 3));
        Assert.Contains("ha 2 pagine", oltre.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => Pdfium.Disegna("non è un pdf"u8.ToArray(), 1));
    }

    [Fact]
    public void Il_png_ha_la_misura_dell_immagine_e_i_suoi_pixel()
    {
        var immagine = Pdfium.Disegna(PaginaColQuadrato(), 1, dpi: 144);
        var png = Png.Scrivi(immagine);

        Assert.Equal((840, 1190), Png.Misura(png));
        Assert.Null(Png.Misura("GIF89a............................"u8));
        // I dati si decomprimono nelle righe filtrate: una riga = 1 byte di filtro + 3 per pixel.
        var idat = png.AsSpan(8 + 25 + 8, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8 + 25, 4))).ToArray();
        using var zlib = new System.IO.Compression.ZLibStream(new MemoryStream(idat), System.IO.Compression.CompressionMode.Decompress);
        using var righe = new MemoryStream();
        zlib.CopyTo(righe);
        Assert.Equal(1190 * (1 + 840 * 3), righe.Length);
    }

    // LIRN: la soglia della 24 e BENTO, più o meno dove stanno (le coordinate servono solo a fare i conti).
    private static readonly Coordinate Soglia = new(40.8936, 14.3086);
    private static readonly Coordinate Lontano = new(41.2500, 14.9000);

    [Fact]
    public void Due_punti_posano_la_carta_e_la_rimettono_al_suo_posto()
    {
        var posa = PosaSullaMappa.Da([
            new PuntoDellaPosa(100, 400, Soglia.LatitudeDeg, Soglia.LongitudeDeg, "RWY 24"),
            new PuntoDellaPosa(300, 150, Lontano.LatitudeDeg, Lontano.LongitudeDeg, "BENTO"),
        ]);

        Assert.NotNull(posa);
        Assert.Null(posa.ScartoMassimoNm);
        Assert.True(PosaSullaMappa.DistanzaNm(posa.SullaTerra(100, 400), Soglia) < 0.001);
        Assert.True(PosaSullaMappa.DistanzaNm(posa.SullaTerra(300, 150), Lontano) < 0.001);
        // Una similitudine non specchia: il nord della pagina (y piccola) è a nord sulla Terra.
        Assert.True(posa.SullaTerra(100, 0).LatitudeDeg > posa.SullaTerra(100, 400).LatitudeDeg);
    }

    [Fact]
    public void Tre_punti_fanno_una_affine_e_dal_quarto_c_e_lo_scarto()
    {
        // Una carta vera: 1 cm = 2 NM, nord in alto. I punti si fanno da una posa nota e se ne sposta uno di 0,3 NM.
        var nota = PosaSullaMappa.Da([
            new PuntoDellaPosa(0, 0, 41.5, 14.0),
            new PuntoDellaPosa(500, 0, 41.5, 14.8),
        ])!;
        PuntoDellaPosa Su(double x, double y) { var c = nota.SullaTerra(x, y); return new(x, y, c.LatitudeDeg, c.LongitudeDeg); }

        var tre = PosaSullaMappa.Da([Su(50, 60), Su(400, 80), Su(200, 500)]);
        Assert.NotNull(tre);
        Assert.Null(tre.ScartoMassimoNm);
        Assert.True(PosaSullaMappa.DistanzaNm(tre.SullaTerra(250, 250), nota.SullaTerra(250, 250)) < 0.01);

        var spostato = Su(300, 300) with { Lat = Su(300, 300).Lat + 0.3 / 60 };
        var quattro = PosaSullaMappa.Da([Su(50, 60), Su(400, 80), Su(200, 500), spostato]);
        Assert.NotNull(quattro);
        Assert.InRange(quattro.ScartoMassimoNm!.Value, 0.05, 0.3);
    }

    [Fact]
    public void Punti_che_non_bastano_non_posano_niente()
    {
        Assert.Null(PosaSullaMappa.Da([]));
        Assert.Null(PosaSullaMappa.Da([new PuntoDellaPosa(1, 1, 41, 14)]));
        Assert.Null(PosaSullaMappa.Da([new PuntoDellaPosa(1, 1, 41, 14), new PuntoDellaPosa(1, 1, 42, 15)]));
        Assert.Null(PosaSullaMappa.Da([new PuntoDellaPosa(0, 0, 41, 14), new PuntoDellaPosa(10, 10, 41.1, 14.1), new PuntoDellaPosa(20, 20, 41.2, 14.2)]));
    }

    [Fact]
    public void Gli_angoli_e_la_scala_della_pagina()
    {
        // 1 cm della pagina (28,35 pt) = 1' di latitudine = 1 NM, nord in alto.
        var punti = 72 / 2.54;
        var posa = PosaSullaMappa.Da([new PuntoDellaPosa(0, 0, 41, 14), new PuntoDellaPosa(0, punti, 41 - 1.0 / 60, 14)])!;

        Assert.Equal(1.0, posa.MigliaPerCentimetro(420, 595), 2);
        var angoli = posa.Angoli(420, 595);
        Assert.Equal(3, angoli.Count);
        Assert.Equal(41, angoli[0].LatitudeDeg, 6);
        Assert.True(angoli[1].LongitudeDeg > 14);      // in alto a destra: a est
        Assert.True(angoli[2].LatitudeDeg < 41);       // in basso a sinistra: a sud
    }

    [Fact]
    public void Le_pose_si_scrivono_per_scalo_una_per_pagina_e_senza_punti_se_ne_vanno()
    {
        var espanso = Directory.CreateTempSubdirectory("posa-").FullName;
        try
        {
            var data = new DateOnly(2025, 10, 2);
            PoseDelleCarte.Scrivi(espanso, "lirn", new PosaDellaCarta("AD 2 LIRN 5-3", "ILS Z RWY 24", data,
                [new PuntoDellaPosa(100.123, 400, 40.89361234, 14.3086, "RWY 24"), new PuntoDellaPosa(300, 150, 41.25, 14.9)]));
            PoseDelleCarte.Scrivi(espanso, "LIRN", new PosaDellaCarta("AD 2 LIRN 2-1", "AERODROME CHART", null, [new PuntoDellaPosa(1, 2, 40.88, 14.29)]));

            var file = PoseDelleCarte.File(espanso, "LIRN");
            Assert.Equal(Path.Combine(espanso, "posa", "LIRN.json"), file);
            var testo = File.ReadAllText(file);
            Assert.True(testo.IndexOf("AD 2 LIRN 2-1", StringComparison.Ordinal) < testo.IndexOf("AD 2 LIRN 5-3", StringComparison.Ordinal));
            Assert.Contains("\"lat\": 40.893612", testo, StringComparison.Ordinal);
            Assert.Contains("\"x\": 100.12", testo, StringComparison.Ordinal);

            var lette = PoseDelleCarte.Leggi(espanso, "LIRN");
            Assert.Equal(2, lette.Count);
            var ils = Assert.Single(lette, p => p.Pagina == "AD 2 LIRN 5-3");
            Assert.Equal(data, ils.Data);
            Assert.Equal("RWY 24", ils.Punti[0].Nome);

            PoseDelleCarte.Scrivi(espanso, "LIRN", ils with { Punti = [] });
            Assert.Single(PoseDelleCarte.Leggi(espanso, "LIRN"));
            PoseDelleCarte.Scrivi(espanso, "LIRN", lette.Single(p => p.Pagina == "AD 2 LIRN 2-1") with { Punti = [] });
            Assert.False(File.Exists(file));
            Assert.Empty(PoseDelleCarte.Leggi(espanso, "LIRN"));
        }
        finally
        {
            Directory.Delete(espanso, recursive: true);
        }
    }
}
