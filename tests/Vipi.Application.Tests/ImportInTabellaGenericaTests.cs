using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// 🔴 U-042 (revisione totale 3): l'import in una tabella generica. Con «la prima riga e' l'intestazione» e «Aggiungi
/// in coda», le colonne incollate prendevano il posto di quelle della tabella e il pareggio tagliava TUTTE le righe:
/// tre colonne incollate su una tabella da quattro, e la quarta spariva dalla bozza senza avviso.
/// </summary>
public class ImportInTabellaGenericaTests
{
    private static readonly string[] Quattro = { "Ente", "Frequenza", "Orario", "Note" };

    private static List<IReadOnlyList<string>> Compilata() => new()
    {
        new[] { "Roma ACC", "125.500", "H24", "via LOA" },
        new[] { "Brindisi ACC", "127.200", "H24", "via LOA" },
    };

    [Fact]
    public void In_coda_con_meno_colonne_le_righe_che_c_erano_restano_intere()
    {
        var (colonne, righe) = TabellaGenerica.Importa(Quattro, Compilata(), new[] { "A", "B", "C" },
            intestazione: true, sostituisci: false, new[] { new[] { "Padova ACC", "120.720", "H24" } });

        Assert.Equal(Quattro, colonne);
        Assert.Equal(new[] { "Roma ACC", "125.500", "H24", "via LOA" }, righe[0]);
        Assert.Equal(new[] { "Padova ACC", "120.720", "H24", "" }, righe[2]);
    }

    [Fact]
    public void In_coda_con_piu_colonne_si_allarga_e_i_nomi_vecchi_restano()
    {
        var (colonne, righe) = TabellaGenerica.Importa(Quattro, Compilata(), new[] { "A", "B", "C", "D", "Fonte" },
            intestazione: true, sostituisci: false, new[] { new[] { "Padova ACC", "120.720", "H24", "", "AIP" } });

        Assert.Equal(new[] { "Ente", "Frequenza", "Orario", "Note", "Fonte" }, colonne);
        Assert.Equal(5, righe[0].Count);
        Assert.Equal("AIP", righe[2][4]);
    }

    [Fact]
    public void In_coda_a_colonne_uguali_l_intestazione_si_usa()
    {
        var (colonne, _) = TabellaGenerica.Importa(Quattro, Compilata(), new[] { "Unit", "Freq", "Hours", "Remarks" },
            intestazione: true, sostituisci: false, System.Array.Empty<IReadOnlyList<string>>());

        Assert.Equal(new[] { "Unit", "Freq", "Hours", "Remarks" }, colonne);
    }

    [Fact]
    public void Sostituendo_con_l_intestazione_la_tabella_prende_le_colonne_nuove()
    {
        var (colonne, righe) = TabellaGenerica.Importa(Quattro, Compilata(), new[] { "A", "B", "C" },
            intestazione: true, sostituisci: true, new[] { new[] { "1", "2", "3" } });

        Assert.Equal(new[] { "A", "B", "C" }, colonne);
        Assert.Single(righe);
    }
}
