using Vipi.Domain.Entities;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Dove stanno i punti scrivibili in un CoP. Due cose sole, e sono tutt'e due decisioni, non dettagli:
/// <b>chi vince fra due omonimi</b> e <b>che cosa succede quando la sorgente e' muta</b>.
///
/// <para>⚠️ Misurato sul <c>vipi.db</c> reale: l'anagrafica ha <b>149 righe, solo VHF e NDB, zero fix</b>, e
/// i CoP veri di Milano (<c>NELAB, ITCAP, LAGEN, EGHIN, KUMIN, VEROB, SIRLO, ASTIG, IXUSA, KUKEV</c>) sono
/// in maggioranza fix di cinque lettere: senza il catalogo del sectorfile la fotografia sarebbe quasi
/// vuota. E' questa misura a decidere che le due sorgenti servono <b>tutt'e due</b>.</para>
/// </summary>
public class CopPositionsTests
{
    [Fact]
    public void Un_nome_sconosciuto_non_si_colloca()
    {
        var p = new CopPositions(new[] { ("NELAB", 45.0, 9.0) });

        Assert.False(p.TryGet("PIPPO", out _));
        Assert.False(p.TryGet("", out _));
        Assert.False(p.TryGet(null, out _));
    }

    [Fact]
    public void Il_nome_si_ripulisce_e_non_guarda_le_maiuscole()
    {
        var p = new CopPositions(new[] { ("NELAB", 45.0, 9.0) });

        Assert.True(p.TryGet("  nelab ", out var punto));
        Assert.Equal(45.0, punto.Lat);
        Assert.Equal(9.0, punto.Lon);
    }

    /// <summary>
    /// ⚠️ La regola che conta: <b>vince la PRIMA occorrenza</b>. E' il modo in cui una coordinata scritta a
    /// mano in anagrafica scavalca quella della sorgente — se vincesse l'ultima, quella valvola non si
    /// aprirebbe mai, e una correzione scritta e mai applicata e' peggio di nessuna valvola.
    /// </summary>
    [Fact]
    public void Fra_due_omonimi_vince_il_primo_accodato()
    {
        var p = new CopPositions(new[] { ("MMP", 45.64, 8.73), ("MMP", 1.0, 2.0) });

        Assert.True(p.TryGet("MMP", out var punto));
        Assert.Equal(45.64, punto.Lat);
        Assert.Equal(1, p.Count);
    }

    [Fact]
    public async Task Il_provider_mette_l_anagrafica_PRIMA_del_catalogo()
    {
        // Stesso codice nelle due sorgenti, con posizioni diverse: deve vincere l'anagrafica (dove la
        // coordinata puo' essere stata scritta a mano).
        var provider = new CopPositionsProvider(
            new AnagraficaFinta(("MMP", 45.64, 8.73)),
            new SorgenteFinta(("MMP", 99.0, 99.0), ("NELAB", 45.5, 9.5)));

        var p = await provider.GetAsync();

        Assert.True(p.TryGet("MMP", out var mmp));
        Assert.Equal(45.64, mmp.Lat);
        Assert.True(p.TryGet("NELAB", out _));   // e i fix arrivano solo dal catalogo
    }

    /// <summary>
    /// Sorgente muta (GitHub giu', o <c>RawBaseUrl</c> vuoto): restano i soli VOR/NDB dell'anagrafica. E' una
    /// <b>degradazione dichiarata</b>, non un guasto — chi risolve perde una risposta e lo dice.
    /// </summary>
    [Fact]
    public async Task Sorgente_muta_lascia_in_piedi_l_anagrafica()
    {
        var provider = new CopPositionsProvider(new AnagraficaFinta(("MMP", 45.64, 8.73)), new SorgenteMuta());

        var p = await provider.GetAsync();

        Assert.Equal(1, p.Count);
        Assert.True(p.TryGet("MMP", out _));
        Assert.False(p.TryGet("NELAB", out _));
    }

    private sealed class AnagraficaFinta : INavaidCatalog
    {
        private readonly IReadOnlyList<NavaidRow> _righe;

        public AnagraficaFinta(params (string Code, double Lat, double Lon)[] punti) =>
            _righe = punti.Select((x, i) => new NavaidRow(
                i + 1, x.Code, "VHF", null, null, null, x.Lat, x.Lon,
                NavaidFieldOrigin.Source, NavaidFieldOrigin.Empty, NavaidFieldOrigin.Source, null, null)).ToList();

        public Task<IReadOnlyList<NavaidRow>> ListAsync(CancellationToken ct = default) => Task.FromResult(_righe);

        public Task<IReadOnlyList<NavaidRow>> GetManyAsync(IReadOnlyList<NavaidKey> keys, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidRow> CreateAsync(string code, string kind, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidDelete> DeleteAsync(int id, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<string>> CitataDaAsync(int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetTypeAsync(int id, string? tipo, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetFrequencyAsync(int id, string? f, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetChannelAsync(int id, string? c, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetCoordinatesAsync(int id, string? s, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidImportOutcome> ImportFromSourceAsync(IReadOnlyList<SourceNavaid> n, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class SorgenteFinta : INavaidSource
    {
        private readonly NavaidCatalog _catalogo;

        public SorgenteFinta(params (string Name, double Lat, double Lon)[] punti) =>
            _catalogo = new NavaidCatalog(punti.Select(p => new NavaidName(p.Name, NavaidKind.Fix, p.Lat, p.Lon)));

        public Task<NavaidCatalog> GetAsync(CancellationToken ct = default) => Task.FromResult(_catalogo);
        public Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default) => Task.FromResult(_catalogo);
    }

    private sealed class SorgenteMuta : INavaidSource
    {
        public Task<NavaidCatalog> GetAsync(CancellationToken ct = default) => Task.FromResult(NavaidCatalog.Empty);
        public Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default) => Task.FromResult(NavaidCatalog.Empty);
    }
}
