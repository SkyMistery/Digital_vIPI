using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vipi.Application.Import;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le <b>ancore</b>: come si legge una tabella copiata da un SOP in PDF, dove di separatori non ce n'e'
/// nessuno e le celle sono fatte di piu' parole.
///
/// <para>Le righe di prova sono quelle vere di un SOP — trattino lungo su quattro righe e trattino normale
/// su una, un doppio spazio in mezzo alla quinta — perche' e' esattamente quel genere di differenza che
/// nessuno vede rileggendo e che fa fallire un'espressione regolare.</para>
/// </summary>
public class ImportAncoreTests
{
    private const string TabellaVera =
        "AIRPORT NAVAIDS BEARING DISTANCE\n" +
        "LIBA Amendola MNL TAC – 99Y 115.25 308° 72.2NM\n" +
        "LIBR Brindisi BRD TAC – 79X 113.20 095° 46.2NM\n" +
        "LICT Trapani TRP TAC – 25X 108.80 229° 268.5NM\n" +
        "LIRM Grazzanise GRA TAC - 122X 111.65 275° 131NM\n" +
        "LIPC Cervia CEV TAC – 83X  113.60 314° 291.3NM";

    private static readonly SpecImport Spec =
        SpecTabelle.AeroportiAlternati("Aeroporto", "Radioassistenze", "Rilevamento", "Distanza");

    [Fact]
    public void La_riga_si_spezza_in_quattro_celle()
    {
        var celle = SpecTabelle.SpezzaAlternato("LIBA Amendola MNL TAC – 99Y 115.25 308° 72.2NM");

        Assert.Equal(new[] { "LIBA Amendola", "MNL TAC - 99Y 115.25", "308", "72.2" }, celle);
    }

    /// <summary>⚠️ Il trattino normale invece di quello lungo, e la distanza senza decimali: la stessa riga
    /// scritta da un altro ufficio.</summary>
    [Fact]
    public void Il_trattino_corto_e_la_distanza_intera_si_leggono_uguale()
    {
        var celle = SpecTabelle.SpezzaAlternato("LIRM Grazzanise GRA TAC - 122X 111.65 275° 131NM");

        Assert.Equal(new[] { "LIRM Grazzanise", "GRA TAC - 122X 111.65", "275", "131" }, celle);
    }

    [Fact]
    public void Il_doppio_spazio_non_sposta_niente()
    {
        var celle = SpecTabelle.SpezzaAlternato("LIPC Cervia CEV TAC – 83X  113.60 314° 291.3NM");

        Assert.Equal(new[] { "LIPC Cervia", "CEV TAC - 83X 113.60", "314", "291.3" }, celle);
    }

    /// <summary>Senza un ident seguito da un tipo d'impianto, il mezzo va tutto alle radioassistenze:
    /// meglio una cella evidentemente da correggere che un nome tagliato a meta' di nascosto.</summary>
    [Fact]
    public void Senza_il_segno_dell_impianto_il_mezzo_resta_intero()
    {
        var celle = SpecTabelle.SpezzaAlternato("LIBG qualcosa di scritto male 120° 30NM");

        Assert.Equal("LIBG", celle![0]);
        Assert.Equal("qualcosa di scritto male", celle[1]);
    }

    [Fact]
    public void Una_riga_senza_i_due_numeri_non_si_spezza() =>
        Assert.Null(SpecTabelle.SpezzaAlternato("LIBA Amendola MNL TAC - 99Y"));

    /// <summary>
    /// La tabella intera: la prima riga e' l'intestazione (si riconosce dai nomi inglesi), le altre cinque
    /// sono dati.
    /// </summary>
    [Fact]
    public async Task La_tabella_vera_si_legge_tutta()
    {
        var righe = TestoTabellare.Righe(TabellaVera);
        var celle = righe.Skip(1).Select(SpecTabelle.SpezzaAlternato).ToList();

        Assert.All(celle, c => Assert.NotNull(c));
        Assert.Equal(new[] { "LIBA Amendola", "LIBR Brindisi", "LICT Trapani", "LIRM Grazzanise", "LIPC Cervia" },
            celle.Select(c => c![0]));

        // E la griglia costruita da quelle celle si legge come una tabella a quattro colonne.
        var griglia = new Griglia(celle.Select(c => (IReadOnlyList<string>)c!).ToList(), FormaGriglia.RigaIntera);
        var p = await CostruttoreProposta.CostruisciAsync(griglia, Spec);

        Assert.Equal(5, p.Righe.Count);
        Assert.Equal("308", p.Righe[0].Celle[2].Valore);
        Assert.Equal("72.2", p.Righe[0].Celle[3].Valore);
        Assert.Equal("131", p.Righe[3].Celle[3].Valore);
    }

    /// <summary>⚠️ L'intestazione della tabella vera e' in inglese: se non si riconoscesse, la prima riga
    /// entrerebbe fra i dati e la tabella nascerebbe con una riga di titoli dentro.</summary>
    [Fact]
    public void L_intestazione_inglese_si_riconosce()
    {
        var g = Griglia.Leggi("AIRPORT\tNAVAIDS\tBEARING\tDISTANCE\nLIBA\tMNL\t308\t72.2");

        Assert.True(MappaturaColonne.Proponi(Spec, g).Intestazione);
    }

    /// <summary>
    /// ⚠️ Il difetto trovato guidando l'app il 2 settembre 2026: incollando la tabella copiata dal PDF, la
    /// riga «AIRPORT NAVAIDS BEARING DISTANCE» non si spezza — le ancore cercano un ICAO e due numeri, e
    /// un'intestazione non ne ha — quindi restava UNA cella sola, finiva fra i dati e si vedeva ROSSA. Chi
    /// importava vedeva una riga illeggibile che era solo il titolo della tabella.
    /// </summary>
    [Fact]
    public void L_intestazione_si_riconosce_anche_quando_resta_in_una_cella_sola()
    {
        var g = new Griglia(
            new IReadOnlyList<string>[] { new[] { "AIRPORT NAVAIDS BEARING DISTANCE" }, new[] { "LIBA Amendola", "MNL", "308", "72.2" } },
            FormaGriglia.RigaIntera);

        Assert.True(MappaturaColonne.Proponi(Spec, g).Intestazione);
    }

    /// <summary>Ma una riga di DATI rimasta in una cella sola non diventa un'intestazione per comodita': non
    /// nomina nessuna colonna, e va vista rossa.</summary>
    [Fact]
    public void Una_riga_di_dati_non_spezzata_resta_un_dato()
    {
        var g = new Griglia(
            new IReadOnlyList<string>[] { new[] { "LIBA Amendola MNL TAC" } }, FormaGriglia.RigaIntera);

        Assert.False(MappaturaColonne.Proponi(Spec, g).Intestazione);
    }

    /// <summary>
    /// ⚠️ Il seguito immediato del difetto qui sopra, e la lezione: un'intestazione RICONOSCIUTA non e'
    /// un'intestazione che dice DOVE. Quella del PDF resta in una cella sola — nomina le colonne una dopo
    /// l'altra ma non ne colloca nessuna — e presa alla lettera lasciava ogni colonna senza posto: righe
    /// tutte vuote. Quando l'intestazione non colloca niente, le colonne si prendono in ordine.
    /// </summary>
    [Fact]
    public void Un_intestazione_che_non_colloca_niente_non_svuota_le_righe()
    {
        var g = new Griglia(
            new IReadOnlyList<string>[]
            {
                new[] { "AIRPORT NAVAIDS BEARING DISTANCE" },
                new[] { "LIBA Amendola", "MNL TAC - 99Y 115.25", "308", "72.2" },
            },
            FormaGriglia.RigaIntera);

        var m = MappaturaColonne.Proponi(Spec, g);

        Assert.True(m.Intestazione);
        Assert.Equal(new[] { 0, 1, 2, 3 }, m.Colonne);
    }
}
