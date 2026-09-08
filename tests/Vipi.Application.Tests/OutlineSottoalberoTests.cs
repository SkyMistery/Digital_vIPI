using Vipi.Application.Content;

namespace Vipi.Application.Tests;

/// <summary>
/// Il sottoalbero di una riga dell'outline: quel che se ne va con lei quando la si elimina.
///
/// <para>🔴 <b>Perché conta.</b> Una riga che se ne va lasciando indietro le proprie eccezioni non lascia
/// «righe in più»: lascia righe che <b>dicono un'altra cosa</b>. L'eccezione di una condizione che non esiste
/// più smette di essere un'eccezione e diventa una clausola qualunque — e nel documento pubblicato ci finisce
/// come se fosse sempre valida. Non è un residuo, è un accordo cambiato in silenzio.</para>
/// </summary>
public sealed class OutlineSottoalberoTests
{
    private sealed record Riga(int Id, int? VariantGroup, int VariantDepth, bool IsGroupWide = false) : IOutlineRow;

    private static IReadOnlyList<Riga> Righe(params Riga[] r) => r;

    [Fact]
    public void Una_riga_senza_gruppo_torna_da_sola()
    {
        var a = new Riga(1, null, 0);
        var righe = Righe(a, new Riga(2, null, 0));

        Assert.Equal(new[] { 1 }, Outline.SubtreeOf(righe, a).Select(x => x.Id));
    }

    [Fact]
    public void Le_alternative_sono_pari_grado_e_non_si_trascinano()
    {
        // Due alternative dello stesso gruppo: nessuna è lo standard dell'altra, quindi nessuna pende dall'altra.
        var a = new Riga(1, 7, 0);
        var b = new Riga(2, 7, 0);
        var righe = Righe(a, b);

        Assert.Equal(new[] { 1 }, Outline.SubtreeOf(righe, a).Select(x => x.Id));
        Assert.Equal(new[] { 2 }, Outline.SubtreeOf(righe, b).Select(x => x.Id));
    }

    [Fact]
    public void Una_riga_si_porta_via_le_proprie_eccezioni_a_ogni_profondita()
    {
        var capofila = new Riga(1, 7, 0);
        var righe = Righe(
            capofila,
            new Riga(2, 7, 1),      // eccezione della 1
            new Riga(3, 7, 2),      // eccezione della 2
            new Riga(4, 7, 1),      // altra eccezione della 1
            new Riga(5, 7, 0));     // l'alternativa dopo: NON è sua

        Assert.Equal(new[] { 1, 2, 3, 4 }, Outline.SubtreeOf(righe, capofila).Select(x => x.Id).Order());
    }

    [Fact]
    public void Una_eccezione_di_mezzo_si_porta_via_solo_il_proprio_ramo()
    {
        var righe = Righe(
            new Riga(1, 7, 0),
            new Riga(2, 7, 1),
            new Riga(3, 7, 2),
            new Riga(4, 7, 1));

        var eccezione = righe.First(r => r.Id == 2);
        Assert.Equal(new[] { 2, 3 }, Outline.SubtreeOf(righe, eccezione).Select(x => x.Id).Order());
    }

    /// <summary>
    /// ⚠️ Chi <b>scavalca</b> le alternative vale per tutto il gruppo: non pende da nessuna, quindi non se ne
    /// va con nessuna. Il contrario — trascinarla via con la prima alternativa eliminata — toglierebbe una
    /// regola che valeva anche per le altre.
    /// </summary>
    [Fact]
    public void Chi_scavalca_le_alternative_non_pende_da_nessuno()
    {
        var capofila = new Riga(1, 7, 0);
        var scavalca = new Riga(2, 7, 1, IsGroupWide: true);
        var righe = Righe(capofila, scavalca, new Riga(3, 7, 1));

        Assert.Equal(new[] { 1, 3 }, Outline.SubtreeOf(righe, capofila).Select(x => x.Id).Order());
        Assert.Equal(new[] { 2 }, Outline.SubtreeOf(righe, scavalca).Select(x => x.Id));
    }

    [Fact]
    public void Le_righe_di_un_ALTRO_gruppo_non_si_toccano()
    {
        var capofila = new Riga(1, 7, 0);
        var righe = Righe(
            capofila,
            new Riga(2, 7, 1),
            new Riga(3, 8, 1),      // stesso aspetto, gruppo diverso
            new Riga(4, 8, 2));

        Assert.Equal(new[] { 1, 2 }, Outline.SubtreeOf(righe, capofila).Select(x => x.Id).Order());
    }

    /// <summary>La riga chiesta è sempre la prima: chi elimina mostra un elenco, e il primo nome è quello che
    /// l'utente ha premuto.</summary>
    [Fact]
    public void La_riga_chiesta_e_sempre_la_prima()
    {
        var righe = Righe(new Riga(1, 7, 0), new Riga(2, 7, 1), new Riga(3, 7, 2));
        var mezzo = righe.First(r => r.Id == 2);

        Assert.Equal(2, Outline.SubtreeOf(righe, mezzo)[0].Id);
    }

    /// <summary>
    /// 🔴 Il sottoalbero deve essere d'accordo con <see cref="Outline.ParentOf"/> <b>riga per riga</b>: sono
    /// la stessa lettura dello stesso outline, e il giorno in cui divergono l'editor mostra un albero e
    /// l'eliminazione ne cancella un altro — senza che la differenza si veda da nessuna parte.
    /// </summary>
    [Fact]
    public void Concorda_con_la_risalita_riga_per_riga()
    {
        var righe = Righe(
            new Riga(1, 7, 0), new Riga(2, 7, 1), new Riga(3, 7, 2), new Riga(4, 7, 1),
            new Riga(5, 7, 0), new Riga(6, 7, 1), new Riga(7, 7, 1, IsGroupWide: true),
            new Riga(8, null, 0), new Riga(9, 8, 0), new Riga(10, 8, 1));

        foreach (var riga in righe)
        {
            var atteso = righe.Where(x => Discende(righe, x, riga)).Select(x => x.Id).Order().ToList();
            var ottenuto = Outline.SubtreeOf(righe, riga).Select(x => x.Id).Order().ToList();
            Assert.Equal(atteso, ottenuto);
        }

        // «x discende da r» detto risalendo, che è l'unica definizione che esiste.
        static bool Discende(IReadOnlyList<Riga> tutte, Riga x, Riga r)
        {
            if (x.Id == r.Id) return true;
            for (var a = Outline.ParentOf(tutte, x); a is not null; a = Outline.ParentOf(tutte, a))
                if (a.Id == r.Id) return true;
            return false;
        }
    }
}
