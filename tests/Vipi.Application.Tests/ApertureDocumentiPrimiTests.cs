using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I tre di ogni gruppo sulla pagina di un ACC (S93, committente 1° ottobre 2026): prima quelli in evidenza a mano,
/// poi i più aperti, poi per nome.
/// </summary>
public class ApertureDocumentiPrimiTests
{
    private sealed record Voce(string Nome, int? InEvidenza, int Aperture);

    private static string[] Primi(params Voce[] voci) =>
        ApertureDocumenti.Primi(voci, v => v.InEvidenza, v => v.Aperture, v => v.Nome).Select(v => v.Nome).ToArray();

    [Fact]
    public void Senza_scelte_a_mano_vincono_i_piu_aperti()
    {
        Assert.Equal(new[] { "LIRP", "LIRQ", "LIRN" },
            Primi(new("LIRA", null, 0), new("LIRN", null, 3), new("LIRP", null, 40), new("LIRQ", null, 12)));
    }

    /// <summary>⚠️ Chi è in evidenza resta in cima anche se lo apre poca gente: la scelta manuale vince sui numeri.</summary>
    [Fact]
    public void Gli_in_evidenza_vengono_prima_nel_loro_ordine_poi_i_piu_aperti()
    {
        Assert.Equal(new[] { "LIRA", "LIRN", "LIRP" },
            Primi(new("LIRP", null, 40), new("LIRN", 2, 0), new("LIRA", 1, 1), new("LIRQ", null, 12)));
    }

    /// <summary>Senza numeri — contatore appena acceso, o archivio irraggiungibile — l'ordine è quello di prima.</summary>
    [Fact]
    public void Senza_aperture_si_torna_all_ordine_alfabetico()
    {
        Assert.Equal(new[] { "LIRA", "LIRN", "LIRP" },
            Primi(new("LIRQ", null, 0), new("LIRP", null, 0), new("LIRA", null, 0), new("LIRN", null, 0)));
    }
}
