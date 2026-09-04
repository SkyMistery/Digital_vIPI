using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La biblioteca degli allegati vista da chi la sfoglia: filtri, ordine e «cartelle».
///
/// <para>⚠️ In produzione ci sono <b>centoventuno</b> voci (contate il 4 settembre 2026), e fino a oggi
/// l'elenco usciva piatto e nell'ordine del database. Queste prove guardano la parte che decide che cosa uno
/// staffista <i>vede</i>: una voce che sparisce dietro un filtro sbagliato è una voce che nessuno ritrova e
/// che qualcuno ricarica in doppio, con due slug diversi per lo stesso PDF.</para>
/// </summary>
public class AttachmentBrowsingTests
{
    private static AttachmentRow Riga(string slug, string titolo, AttachmentKind tipo,
        AttachmentScope ambito, string? chiave, string? note = null) =>
        new(0, slug, titolo, tipo, ambito, chiave, note, 1, 1,
            AttachmentProvider.Drive, "x", DateTime.UtcNow, DateTimeUtc());

    private static DateTime DateTimeUtc() => new(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<AttachmentRow> Archivio() => new[]
    {
        Riga("loa-lirr-lfmm", "LoA Roma–Marseille", AttachmentKind.Loa, AttachmentScope.Acc, "LIRR"),
        Riga("loa-limm-lsaz", "LoA Milano–Zurigo", AttachmentKind.Loa, AttachmentScope.Acc, "LIMM"),
        Riga("circ-limm-01", "Circolare Milano 01", AttachmentKind.Circular, AttachmentScope.Acc, "LIMM"),
        Riga("chart-limc-ad", "Carta di aerodromo Malpensa", AttachmentKind.Chart, AttachmentScope.Airport, "LIMC"),
        Riga("piv-it", "PIV Italia", AttachmentKind.Piv, AttachmentScope.Division, null, note: "documento di divisione"),
    };

    private static bool Tutte(string _) => true;
    private static bool Nessuna(string _) => false;

    [Fact] // il terzo asse: «gli allegati di Milano», che con due soli assi non si poteva chiedere
    public void La_chiave_del_perimetro_filtra_da_sola()
    {
        var r = AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(ScopeKey: "LIMM"), Tutte);

        Assert.Equal(2, r.Count);
        Assert.All(r, x => Assert.Equal("LIMM", x.ScopeKey));
    }

    [Fact] // gli assi stanno in AND: tipo E chiave insieme
    public void I_tre_assi_si_sommano()
    {
        var r = AttachmentBrowsing.Filtra(Archivio(),
            new AttachmentFilter(Kind: AttachmentKind.Loa, Scope: AttachmentScope.Acc, ScopeKey: "LIMM"), Tutte);

        Assert.Single(r);
        Assert.Equal("loa-limm-lsaz", r[0].Slug);
    }

    [Fact] // la ricerca guarda tutto quel che si legge: titolo, slug, chiave e note
    public void La_ricerca_guarda_titolo_slug_chiave_e_note()
    {
        Assert.Single(AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(Search: "Marseille"), Tutte));
        Assert.Single(AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(Search: "loa-limm"), Tutte));
        Assert.Single(AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(Search: "limc"), Tutte));
        Assert.Single(AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(Search: "di divisione"), Tutte));
    }

    [Fact] // il chip «mai usata»: la risposta la sa il chiamante, qui si guarda solo che la usi
    public void Il_chip_mai_usata_tiene_solo_quelle_che_nessuno_cita()
    {
        Assert.Empty(AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(OnlyUnused: true), Tutte));
        Assert.Equal(5, AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(OnlyUnused: true), Nessuna).Count);
    }

    [Fact] // ⚠️ l'ordine È parte del filtro: prima usciva quello del database, che a 121 voci non è un ordine
    public void L_elenco_esce_ordinato_per_perimetro_poi_tipo_poi_titolo()
    {
        var r = AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(), Tutte);

        // Divisione, poi gli ACC per chiave (LIMM prima di LIRR) e dentro ciascuno per tipo, poi gli scali.
        Assert.Equal(
            new[] { "piv-it", "loa-limm-lsaz", "circ-limm-01", "loa-lirr-lfmm", "chart-limc-ad" },
            r.Select(x => x.Slug).ToArray());
    }

    [Fact] // le «cartelle»: un gruppo per perimetro, nell'ordine dell'elenco
    public void Le_righe_si_raccolgono_per_perimetro()
    {
        var gruppi = AttachmentBrowsing.Raggruppa(
            AttachmentBrowsing.Filtra(Archivio(), new AttachmentFilter(), Tutte));

        Assert.Equal(4, gruppi.Count);
        Assert.Equal(AttachmentScope.Division, gruppi[0].Scope);
        Assert.Null(gruppi[0].ScopeKey);
        Assert.Equal("div", gruppi[0].Key);

        // Milano ha due voci e non due gruppi: il perimetro è (ambito, chiave), non l'una o l'altra.
        var milano = gruppi.Single(g => g.ScopeKey == "LIMM");
        Assert.Equal(2, milano.Rows.Count);
        Assert.Equal("Acc:LIMM", milano.Key);
    }

    [Fact] // i chip dicono dove ci sono DAVVERO allegati, e li contano
    public void Le_chiavi_presenti_escono_in_ordine_col_conto()
    {
        var chiavi = AttachmentBrowsing.Chiavi(Archivio());

        Assert.Equal(new[] { "LIMC", "LIMM", "LIRR" }, chiavi.Select(c => c.Chiave).ToArray());
        Assert.Equal(2, chiavi.Single(c => c.Chiave == "LIMM").Quante);
    }

    [Fact] // ⚠️ la divisione non ha chiave: non deve inventarne una vuota fra i chip
    public void La_divisione_non_produce_un_chip_vuoto()
    {
        var chiavi = AttachmentBrowsing.Chiavi(Archivio());
        Assert.DoesNotContain(chiavi, c => string.IsNullOrWhiteSpace(c.Chiave));
    }
}
