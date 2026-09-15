using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il corpo di una sezione come una fila sola di blocchi e sotto-sezioni (15 settembre 2026, committente:
/// «sottosezione, blocco, sottosezione, blocco»).
/// </summary>
public class CorpoDiSezioneTests
{
    private sealed record B(int Id, int Order);
    private sealed record F(int Id, int? Pos, bool Before = false);

    private static string Fila(IEnumerable<B> blocchi, IEnumerable<F> figlie, bool scheda = false) =>
        string.Join(" ", CorpoDiSezione.Componi(blocchi, b => b.Order, figlie,
                f => CorpoDiSezione.Soglia(f.Pos, f.Before), scheda)
            .Select(v => v.Tipo switch
            {
                TipoVoce.Blocco => "b" + v.Blocco!.Id,
                TipoVoce.Figlia => "f" + v.Figlia!.Id,
                _ => "S",
            }));

    [Fact]
    public void Senza_posizione_vale_il_flag_storico_prima_blocchi_dopo()
    {
        // I documenti e le release scritti prima: nessuno deve cambiare aspetto da solo.
        Assert.Equal("f1 b10 b11 f2",
            Fila(new[] { new B(10, 1), new B(11, 2) }, new[] { new F(1, null, Before: true), new F(2, null) }));
        Assert.Equal("f1 S b10 f2",
            Fila(new[] { new B(10, 1) }, new[] { new F(1, null, Before: true), new F(2, null) }, scheda: true));
    }

    [Fact]
    public void Figlie_e_blocchi_si_alternano_sulla_soglia()
    {
        Assert.Equal("f1 b10 f2 b11 f3",
            Fila(new[] { new B(10, 1), new B(11, 2) }, new[] { new F(1, 0), new F(2, 1), new F(3, 2) }));
    }

    [Fact]
    public void Un_blocco_cancellato_non_sposta_le_figlie()
    {
        // Order 1, 3: il 2 è stato cancellato. La figlia «dopo il blocco 2» resta prima del blocco 3.
        Assert.Equal("b10 f1 b12", Fila(new[] { new B(10, 1), new B(12, 3) }, new[] { new F(1, 2) }));
    }

    [Fact]
    public void Un_blocco_non_scavalca_la_scheda()
    {
        var fila = new[] { new VoceCorpo(TipoVoce.Scheda, 0), new VoceCorpo(TipoVoce.Blocco, 10) };
        Assert.Null(CorpoDiSezione.Sposta(fila, 1, -1));
        Assert.Null(CorpoDiSezione.Sposta(fila, 0, -1));   // ai bordi
    }

    [Fact]
    public void La_figlia_scende_sotto_un_blocco_e_la_fila_si_riscrive()
    {
        // f1 b10 b11  →  freccia giù su f1  →  b10 f1 b11
        var fila = new[]
        {
            new VoceCorpo(TipoVoce.Figlia, 1), new VoceCorpo(TipoVoce.Blocco, 10), new VoceCorpo(TipoVoce.Blocco, 11),
        };
        var nuova = CorpoDiSezione.Sposta(fila, 0, 1)!;
        var piano = CorpoDiSezione.Pianifica(nuova, new[] { (10, 1), (11, 2) });

        Assert.Equal(new[] { (10, 1), (11, 2) }, piano.Blocchi);
        Assert.Equal(new[] { (1, 1, 1) }, piano.Figlie);
        Assert.Equal("b10 f1 b11", Fila(new[] { new B(10, 1), new B(11, 2) }, new[] { new F(1, 1) }));
    }

    [Fact]
    public void Due_blocchi_si_scambiano_e_il_payload_nascosto_resta_al_suo_posto()
    {
        // Blocco 5 = payload della scheda (Order 1), non in fila. La fila mostra b10 b11: scambiati.
        var fila = new[] { new VoceCorpo(TipoVoce.Scheda, 0), new VoceCorpo(TipoVoce.Blocco, 11), new VoceCorpo(TipoVoce.Blocco, 10) };
        var piano = CorpoDiSezione.Pianifica(fila, new[] { (5, 1), (10, 2), (11, 3) });
        Assert.Equal(new[] { (5, 1), (11, 2), (10, 3) }, piano.Blocchi);
    }

    [Fact]
    public void Prima_della_scheda_e_in_testa_dopo_la_scheda_e_zero()
    {
        var fila = new[]
        {
            new VoceCorpo(TipoVoce.Figlia, 1), new VoceCorpo(TipoVoce.Scheda, 0), new VoceCorpo(TipoVoce.Figlia, 2),
            new VoceCorpo(TipoVoce.Blocco, 10), new VoceCorpo(TipoVoce.Figlia, 3),
        };
        var piano = CorpoDiSezione.Pianifica(fila, new[] { (10, 4) });
        Assert.Equal(new[] { (1, CorpoDiSezione.InTesta, 1), (2, 0, 2), (3, 1, 3) }, piano.Figlie);
        Assert.Equal("f1 S f2 b10 f3",
            Fila(new[] { new B(10, 1) }, new[] { new F(1, -1), new F(2, 0), new F(3, 1) }, scheda: true));
    }

    [Fact]
    public void Un_blocco_aggiunto_in_fondo_va_dopo_le_figlie_posizionate()
    {
        Assert.Equal("b10 f1 b11", Fila(new[] { new B(10, 1), new B(11, 2) }, new[] { new F(1, 1) }));
    }
}
