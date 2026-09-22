using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Ui.Server;

/// <summary>
/// La geometria per la mappa, uno strato per volta (carta F3 §3): NON passa dal circuito SignalR, che ha un tetto di
/// 32 KB in ricezione e porterebbe megabyte di coordinate in messaggi — la pagina la chiede con una <c>fetch</c>, e
/// il cancello vale anche qui (senza cookie, 403 come tutto il resto).
/// <para>Il JSON è corto di proposito: chiavi di una lettera e coordinate a <b>cinque</b> decimali (circa un metro).
/// L'albero vero è 244 227 punti: scritti per esteso sarebbero decine di MB da serializzare a ogni accensione.</para>
/// </summary>
public static class MappaDelLab
{
    /// <summary>Quanti decimali di grado: 5 ≈ 1,1 m in latitudine. Il sector stesso non è più preciso di così.</summary>
    public const int Decimali = 5;

    public static void MappaLaMappa(IEndpointRouteBuilder rotte)
    {
        ArgumentNullException.ThrowIfNull(rotte);

        // L'elenco degli strati con i loro numeri: la pagina lo mostra nelle caselle, senza scaricare le coordinate.
        rotte.MapGet("/mappa/strati", (SessioneDelLab lab) => Results.Json(
            lab.Strati.Select(s => new
            {
                id = s.Id,
                nome = s.Tipo.Nome,
                sfondo = s.Tipo.Sfondo,
                forme = s.Forme.Count,
                punti = s.Punti,
            })));

        rotte.MapGet("/mappa/strato/{id}", (string id, SessioneDelLab lab, HttpResponse risposta) =>
        {
            if (StratiDellaMappa.PerNome(id) is null)
                return Results.NotFound();

            var strato = lab.Strati.FirstOrDefault(s => s.Id == id);
            // Sessione chiusa o strato senza forme: si risponde con l'elenco vuoto, non con un errore — la pagina
            // accende una casella e non deve distinguere «non c'è niente» da «è andata male».
            return Results.Stream(
                flusso => Scrivi(flusso, id, strato?.Forme ?? []),
                contentType: "application/json");
        });
    }

    /// <summary>Scrive uno strato nel formato corto. Pubblica perché la misura sull'albero vero la chiama da fuori.</summary>
    public static async Task Scrivi(Stream flusso, string id, IReadOnlyList<FormaDellaMappa> forme)
    {
        await using var json = new Utf8JsonWriter(flusso);
        json.WriteStartObject();
        json.WriteString("id", id);
        json.WriteStartArray("f");

        foreach (var forma in forme)
        {
            json.WriteStartObject();
            json.WriteString("p", forma.File);
            json.WriteNumber("r", forma.Record);
            json.WriteString("t", forma.Tipo switch
            {
                TipoDiForma.Punto => "p",
                TipoDiForma.Linea => "l",
                _ => "a",
            });
            json.WriteString("e", forma.Etichetta);

            json.WriteStartArray("c");
            foreach (var tratto in forma.Tratti)
            {
                json.WriteStartArray();
                foreach (var punto in tratto)
                {
                    json.WriteNumberValue(Math.Round(punto.LatitudeDeg, Decimali));
                    json.WriteNumberValue(Math.Round(punto.LongitudeDeg, Decimali));
                }

                json.WriteEndArray();
            }

            json.WriteEndArray();

            // I nomi che il catalogo non conosce viaggiano con la forma: è ciò che spiega un disegno mancante
            // (slice 3b: una forma senza punti resta, col nome, invece di sparire in silenzio).
            if (forma.NomiNonRisolti.Count > 0)
            {
                json.WriteStartArray("x");
                foreach (string nome in forma.NomiNonRisolti)
                    json.WriteStringValue(nome);
                json.WriteEndArray();
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
        await json.FlushAsync().ConfigureAwait(false);
    }
}
