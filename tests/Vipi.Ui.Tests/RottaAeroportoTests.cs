using Vipi.Application.Content;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le pagine d'aeroporto e di vSOP militare stanno sotto <c>/services/vsop/{acc}/…</c> ma risolvono il
/// documento per <b>ICAO</b>: l'ACC nella rotta è chrome. Quando uno scalo cambia centro, i link scritti
/// prima continuano quindi a funzionare mostrando l'ACC <b>sbagliato</b> — una pagina che dice il falso senza
/// rompersi. Qui si decide quando mandare l'indirizzo a quello giusto.
/// </summary>
public class RottaAeroportoTests
{
    private sealed class CatalogoFinto : IStationResolver
    {
        private readonly Dictionary<string, string> _acc;
        public CatalogoFinto(params (string Icao, string Acc)[] aeroporti) =>
            _acc = aeroporti.ToDictionary(x => x.Icao, x => x.Acc, StringComparer.OrdinalIgnoreCase);

        public AirportStation? Airport(string? icao) =>
            icao is not null && _acc.TryGetValue(icao, out var acc) ? new AirportStation(icao, acc) : null;

        public IReadOnlyList<AccInfo> Accs => Array.Empty<AccInfo>();
        public AccInfo? Resolve(string accCode) => null;
        public AccInfo? ResolveByCallsign(string callsign) => null;
        public AirportStation? AirportOfCallsign(string? callsign) => null;
        public void Prewarm() { }
    }

    private static readonly CatalogoFinto Catalogo = new(("LIBD", "LIRR"), ("LIRF", "LIRR"));

    [Fact]
    public void L_indirizzo_col_centro_di_prima_si_corregge()
    {
        Assert.Equal("LIRR", RottaAeroporto.AccDaCorreggere(Catalogo, "libb", "LIBD"));
    }

    [Theory]
    [InlineData("LIRR")]
    [InlineData("lirr")]   // l'indirizzo è minuscolo: non è un ACC diverso
    public void L_indirizzo_giusto_si_lascia_stare(string acc)
    {
        Assert.Null(RottaAeroporto.AccDaCorreggere(Catalogo, acc, "LIBD"));
    }

    [Fact]
    public void Un_ICAO_che_il_catalogo_non_conosce_non_manda_da_nessuna_parte()
    {
        // Non si sa dove starebbe: un rimando sarebbe un'invenzione, e la pagina sa già dire «non trovato».
        Assert.Null(RottaAeroporto.AccDaCorreggere(Catalogo, "libb", "ZZZZ"));
        Assert.Null(RottaAeroporto.AccDaCorreggere(Catalogo, "libb", null));
    }
}
