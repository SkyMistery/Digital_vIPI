using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La difesa a <b>due mani</b> contro il catch-all dell'aeroporto (carta
/// <c>2026-08-27-vsop-militari.md</c> §7.1).
///
/// <para>
/// <c>AirportReleaseTarget.TryDescribe</c> accetta <b>qualunque</b> <c>Document</c> vIPI non riconosciuto
/// come APP o ACC: è il catch-all, e ha l'ordine più alto. Senza intervento ogni documento militare ci
/// finirebbe dentro <b>in silenzio</b>, e la diagnosi sarebbe «l'aeroporto mostra il documento sbagliato» —
/// lo stesso guasto già pagato con l'APP non remotizzato.
/// </para>
///
/// <para>
/// ⚠️ <b>Serve un test che lo pretenda, non solo il codice</b>: senza, la regressione è muta. Il catch-all
/// non fallisce — <i>risponde</i>.
/// </para>
/// </summary>
public class CatchAllEdizioneMilitareTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static Document Doc(DocumentEdition edizione, Airport? civile = null, Airport? militare = null) =>
        new()
        {
            Id = 1, Type = DocumentType.Vipi, Title = "T", Language = Language.It,
            Edition = edizione, LastUpdatedAiracCycle = "2609",
            Airport = civile, MilAirport = militare,
        };

    private static Airport Scalo() => new() { Icao = "LIPI", Name = "Rivolto" };

    // ---- La prima mano: l'ordine ---------------------------------------------------------------------

    [Fact]
    public void I_descrittori_militari_vengono_interrogati_PRIMA_di_tutti()
    {
        // Da solo l'ordine non basta -- un documento militare che i descrittori militari NON riconoscono
        // ricadrebbe comunque nel catch-all -- ma senza, il controllo sull'edizione non verrebbe mai
        // raggiunto: il catch-all civile risponderebbe per primo.
        // ⚠️ «PRIMA DI TUTTI» va provato contro TUTTI, non contro il solo catch-all: fino al 29 agosto 2026
        // questo test guardava soltanto `AirportReleaseTarget`, e intanto `VloaReleaseTarget` stava a zero —
        // cioè PARI con i militari. Non faceva danno (la vLOA rifiuta per `doc.Type`), ma la difesa era più
        // debole di come la carta la racconta, e nessuno l'avrebbe saputo.
        var militari = new[]
        {
            new AirportMilReleaseTarget(_db).DescribeOrder,
            new AppMilReleaseTarget(_db).DescribeOrder,
        };
        var civili = new[]
        {
            new AirportReleaseTarget(_db).DescribeOrder,
            new AppReleaseTarget(_db).DescribeOrder,
            new AccVipiReleaseTarget(_db).DescribeOrder,
            new VloaReleaseTarget(_db).DescribeOrder,
        };

        Assert.All(militari, m => Assert.All(civili, c => Assert.True(m < c,
            $"un descrittore militare (ordine {m}) deve essere interrogato prima di ogni civile (ordine {c}).")));
    }

    // ---- La seconda mano: il controllo sull'edizione --------------------------------------------------

    [Fact]
    public void IL_CATCH_ALL_RIFIUTA_un_documento_militare()
    {
        // ⚠️ È IL test della slice. Se cade, i documenti militari tornano a essere descritti come vIPI
        // civili d'aeroporto: stessa chiave di release, stesso bersaglio, e le due edizioni si
        // sovrascrivono a vicenda senza che niente protesti.
        var doc = Doc(DocumentEdition.Military, militare: Scalo());
        Assert.False(new AirportReleaseTarget(_db).TryDescribe(doc, hasDraft: false, out _));
    }

    [Fact]
    public void Anche_gli_altri_descrittori_CIVILI_rifiutano_il_militare()
    {
        // È la metà che si dimentica: aggiungere il controllo ai soli descrittori militari lascerebbe i
        // civili disposti ad accettare un documento militare.
        var doc = Doc(DocumentEdition.Military, militare: Scalo());
        Assert.False(new AppReleaseTarget(_db).TryDescribe(doc, false, out _));
        Assert.False(new AccVipiReleaseTarget(_db).TryDescribe(doc, false, out _));

        var vloa = new Document
        {
            Id = 2, Type = DocumentType.Vloa, Title = "L", Language = Language.En,
            Edition = DocumentEdition.Military, LastUpdatedAiracCycle = "2609",
        };
        Assert.False(new VloaReleaseTarget(_db).TryDescribe(vloa, false, out _));
    }

    // ---- Il descrittore militare fa il suo mestiere ---------------------------------------------------

    [Fact]
    public void Il_descrittore_militare_riconosce_il_SUO_documento()
    {
        var doc = Doc(DocumentEdition.Military, militare: Scalo());
        Assert.True(new AirportMilReleaseTarget(_db).TryDescribe(doc, false, out var managed));
        Assert.Equal(ReleaseTargetType.AirportMil, managed.Kind);
        Assert.Equal("LIPI", managed.ReleaseKey);
    }

    [Fact]
    public void Il_descrittore_militare_rifiuta_un_documento_CIVILE()
    {
        var doc = Doc(DocumentEdition.Civil, civile: Scalo());
        Assert.False(new AirportMilReleaseTarget(_db).TryDescribe(doc, false, out _));
    }

    [Fact]
    public void Un_documento_militare_senza_aeroporto_non_e_dell_aeroporto()
    {
        // Sarà l'edizione militare di un APP: il descrittore d'aeroporto deve lasciarlo passare oltre
        // invece di descriverlo con un ICAO vuoto — che lo renderebbe irraggiungibile senza dare errore.
        var doc = Doc(DocumentEdition.Military);
        Assert.False(new AirportMilReleaseTarget(_db).TryDescribe(doc, false, out _));
    }

    // ---- Le due edizioni convivono --------------------------------------------------------------------

    [Fact]
    public void Le_due_edizioni_dello_stesso_scalo_hanno_release_DISTINTE()
    {
        // ⚠️ È il fatto che ha reso possibile il documento separato: l'identità di una release è
        // (TargetType, TargetKey), quindi «AirportMil|LIPI» e «Airport|LIPI» convivono con progressivi e
        // cicli AIRAC indipendenti. Zero lavoro, ma va provato: se un giorno la chiave diventasse la sola
        // TargetKey, le due edizioni si sovrascriverebbero.
        var civile = Doc(DocumentEdition.Civil, civile: Scalo());
        var militare = Doc(DocumentEdition.Military, militare: Scalo());

        Assert.True(new AirportReleaseTarget(_db).TryDescribe(civile, false, out var a));
        Assert.True(new AirportMilReleaseTarget(_db).TryDescribe(militare, false, out var b));

        Assert.Equal(a.ReleaseKey, b.ReleaseKey);   // stessa chiave: l'ICAO
        Assert.NotEqual(a.Kind, b.Kind);       // tipo diverso: e' questo che li tiene separati
    }

    // ---- I valori dell'enum ---------------------------------------------------------------------------

    [Fact]
    public void I_tipi_militari_stanno_IN_CODA_all_enum()
    {
        // ⚠️ Nel payload di release gli enum sono ORDINALI, non nomi come nelle colonne del database:
        // inserirne uno in mezzo reinterpreterebbe in silenzio OGNI release già pubblicata — una vLOA
        // diventerebbe una vIPI ACC senza che nulla protesti.
        Assert.Equal(0, (int)ReleaseTargetType.Vloa);
        Assert.Equal(1, (int)ReleaseTargetType.AccVipi);
        Assert.Equal(2, (int)ReleaseTargetType.App);
        Assert.Equal(3, (int)ReleaseTargetType.Airport);
        Assert.Equal(4, (int)ReleaseTargetType.AirportMil);
        Assert.Equal(5, (int)ReleaseTargetType.AppMil);
    }

    [Fact]
    public void Civil_e_lo_zero_dell_edizione()
    {
        // Ogni documento esistente nasce civile senza toccare una riga, e il default della colonna
        // coincide con quello del modello.
        Assert.Equal(0, (int)DocumentEdition.Civil);
        Assert.Equal(DocumentEdition.Civil, new Document { Title = "x", LastUpdatedAiracCycle = "2609" }.Edition);
    }
}
