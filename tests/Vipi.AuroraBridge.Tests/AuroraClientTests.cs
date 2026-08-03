using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Client del protocollo Aurora contro un server finto. Coperti i tre vincoli scoperti in F0: correlazione
/// per eco del comando, messaggi non sollecitati in mezzo, e il rifiuto della scrittura su traffico non assunto.
/// </summary>
public class AuroraClientTests
{
    private static AuroraClient Connect(FakeAuroraServer server, int timeoutMs = 1500) =>
        new(new AuroraClientOptions("127.0.0.1", server.Port, timeoutMs));

    [Fact]
    public async Task Comando_semplice_e_risposta()
    {
        await using var server = new FakeAuroraServer().Reply("#CONN", "#CONN;LIZZ_AEW_CTR");
        await using var client = Connect(server);

        var response = await client.SendAsync("#CONN");

        Assert.True(response.Ok);
        Assert.Equal("LIZZ_AEW_CTR", response.Fields[0]);
    }

    [Fact]
    public async Task Gli_argomenti_finiscono_nel_pacchetto_separati_da_punto_e_virgola()
    {
        await using var server = new FakeAuroraServer().Reply("#LBALT", "#LBALT;FDX126;250");
        await using var client = Connect(server);

        await client.SendAsync("#LBALT", CancellationToken.None, "FDX126", "250");

        Assert.Contains("#LBALT;FDX126;250", server.Received);
    }

    [Fact]
    public async Task Un_argomento_con_punto_e_virgola_non_parte_nemmeno()
    {
        await using var server = new FakeAuroraServer();
        await using var client = Connect(server);

        var response = await client.SendAsync("#LBALT", CancellationToken.None, "FDX126", "per aerovia; come da LoA");

        Assert.False(response.Ok);
        Assert.Contains("«;»", response.Error);
        Assert.Empty(server.Received);   // il protocollo si romperebbe: non si invia proprio
    }

    [Fact]
    public async Task Errore_di_Aurora_diventa_un_esito_negativo_col_messaggio()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#LBALT", "@ERR;#LBALT;RYR90RC;250;Traffic not assumed.");
        await using var client = Connect(server);

        var response = await client.SendAsync("#LBALT", CancellationToken.None, "RYR90RC", "250");

        Assert.False(response.Ok);
        Assert.Equal("Traffic not assumed.", response.Error);
    }

    [Fact]
    public async Task Comando_sconosciuto_e_riconosciuto_come_tale()
    {
        await using var server = new FakeAuroraServer();   // il finto risponde @ERR;…;Unknown command
        await using var client = Connect(server);

        var response = await client.SendAsync("#LBXFL");

        Assert.False(response.Ok);
        Assert.Equal("Unknown command", response.Error);
    }

    [Fact]
    public async Task I_messaggi_non_sollecitati_non_vengono_scambiati_per_la_risposta()
    {
        await using var server = new FakeAuroraServer().Reply("#SELTFC", "#SELTFC;FDX126;");
        server.PushBeforeReply.Add("#INTERCOMPHONESTATUS;PHONE_RECEIVING");
        server.PushBeforeReply.Add("#INTERCOMCALLSTATUS;CALL_RESULT_IN_OK;LIRR_NE_CTR;;");

        await using var client = Connect(server);
        var pushed = new List<string>();
        client.Unsolicited += pushed.Add;

        var response = await client.SendAsync("#SELTFC");

        Assert.True(response.Ok);
        Assert.Equal("FDX126", response.Fields[0]);
        Assert.Equal(2, pushed.Count);
    }

    [Fact]
    public async Task Il_silenzio_scade_e_non_blocca_il_tool()
    {
        await using var server = new FakeAuroraServer();
        server.Silent.Add("#TR");
        await using var client = Connect(server, timeoutMs: 300);

        var response = await client.SendAsync("#TR");

        Assert.False(response.Ok);
        Assert.Contains("Nessuna risposta", response.Error);
    }

    [Fact]
    public async Task Aurora_chiusa_produce_un_errore_leggibile_non_un_eccezione()
    {
        // Porta quasi certamente libera: nessun listener.
        await using var client = new AuroraClient(new AuroraClientOptions("127.0.0.1", 1, 300));

        var response = await client.SendAsync("#CONN");

        Assert.False(response.Ok);
        Assert.Contains("non raggiungibile", response.Error);
    }

    [Fact]
    public async Task Richieste_in_sequenza_non_si_mescolano()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#TRPOS;A", "#TRPOS;A;1;1;1000;100;40.0;9.0;2000;;;;;;;0;0;0;;1;;0;;")
            .Reply("#TRPOS;B", "#TRPOS;B;2;2;2000;200;41.0;10.0;2000;;;;;;;0;0;0;;1;;0;;");
        await using var client = Connect(server);

        // Lanciate insieme: il client le serializza, quindi ognuna riceve LA SUA risposta.
        var first = client.SendAsync("#TRPOS", CancellationToken.None, "A");
        var second = client.SendAsync("#TRPOS", CancellationToken.None, "B");
        await Task.WhenAll(first, second);

        Assert.Equal("A", (await first).Fields[0]);
        Assert.Equal("B", (await second).Fields[0]);
    }
}
