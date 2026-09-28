using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Le famiglie dichiarate (lotto «Subito» slice 8b, D5): lo stesso <c>form=NOME</c> in più record; chi non ha la forma
/// della famiglia è una «copia di forma diversa» (avviso).
/// </summary>
public sealed class FamiglieDichiarateTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Confine = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Mappe = "SectorFiles/Include/IT/zzzz.str";

    private static readonly string[] Quadrato =
        ["N041.00.00.000;E012.00.00.000;", "N041.10.00.000;E012.00.00.000;", "N041.10.00.000;E012.10.00.000;", "N041.00.00.000;E012.10.00.000;"];

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private void Scrivi(string file, params string[] righe) => _albero.Scrivi(file, string.Join("\r\n", righe) + "\r\n");

    private void ScriviIlSettore(string? famiglia)
        => Scrivi(Settore, [.. famiglia is null ? [] : new[] { $"//@\"LZZZ_APP\" form={famiglia}", "//@START" },
            "LZZZ_APP;APP;1;APP;1;", .. Quadrato, .. famiglia is null ? [] : new[] { "//@END \"LZZZ_APP\"" }]);

    private void ScriviIlConfine(string? famiglia, int giro = 0)
        => Scrivi(Confine, [.. famiglia is null ? [] : new[] { $"//@\"ZZ CONF\" form={famiglia}", "//@START" },
            .. Quadrato.Skip(giro).Concat(Quadrato.Take(giro)).Select(v => "T;ZZ CONF;" + v),
            .. famiglia is null ? [] : new[] { "//@END \"ZZ CONF\"" }]);

    private (SessioneAperta Sessione, IReadOnlyList<FamigliaDichiarata> Famiglie) Leggi()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var forme = FormeUguali.Di(StratiDellaMappa.DiSessione(sessione, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]));
        return (sessione, FamiglieDichiarate.Di(sessione, forme));
    }

    [Fact]
    public void LoStessoNomeFaUnaFamigliaEUgualeNonDaAvvisi()
    {
        ScriviIlSettore("ZZ");
        ScriviIlConfine("ZZ", giro: 2);

        var (sessione, famiglie) = Leggi();

        var famiglia = Assert.Single(famiglie);
        Assert.Equal("ZZ", famiglia.Nome);
        Assert.Equal([(Settore, 0, true), (Confine, 0, true)], famiglia.Membri.Select(m => (m.File, m.Record, m.Uguale)));
        Assert.False(famiglia.ConCopieDiverse);
        Assert.Empty(FamiglieDichiarate.Problemi(famiglie, sessione));
        Assert.Same(famiglia, FamiglieDichiarate.Del(famiglie, sessione, Settore, 0));
    }

    [Fact]
    public void UnMembroConUnVerticeSpostatoEUnaCopiaDiFormaDiversa()
    {
        ScriviIlSettore("ZZ");
        ScriviIlConfine("ZZ");
        Scrivi(Mappe, "//@\"ZZZZ CTR\" form=ZZ", "//@START", "ZZZZ;MAPS;ZZZZ CTR;;;;;1;",
            Quadrato[0], Quadrato[1], "N041.10.00.000;E012.11.00.000;", Quadrato[3], "//@END \"ZZZZ CTR\"");

        var (sessione, famiglie) = Leggi();

        var famiglia = Assert.Single(famiglie);
        Assert.True(famiglia.ConCopieDiverse);
        Assert.False(famiglia.Membri.Single(m => m.File == Mappe).Uguale);
        var problema = Assert.Single(FamiglieDichiarate.Problemi(famiglie, sessione));
        Assert.Equal(Regola.FormeDiverse, problema.Regola);
        Assert.Equal(Gravita.Avviso, problema.Gravita);
        Assert.Equal((Mappe, 3, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;"), (problema.File, problema.Riga, problema.Testo));
        Assert.Equal("Famiglia «ZZ»: ZZZZ CTR ha una forma diversa da prova.tfl LZZZ_APP, prova.hartcc ZZ CONF.", problema.Dettaglio);
    }

    [Fact]
    public void SenzaTagNienteFamigliaAncheSeLeFormeSonoUguali()
    {
        ScriviIlSettore(null);
        ScriviIlConfine(null);

        var (sessione, famiglie) = Leggi();

        Assert.Empty(famiglie);
        Assert.Null(FamiglieDichiarate.Del(famiglie, sessione, Settore, 0));
    }

    [Fact]
    public void UnaFamigliaDiUnoNonDaAvvisi()
    {
        ScriviIlSettore("SOLO");
        ScriviIlConfine(null);

        var (sessione, famiglie) = Leggi();

        Assert.Single(Assert.Single(famiglie).Membri);
        Assert.Empty(FamiglieDichiarate.Problemi(famiglie, sessione));
    }
}
