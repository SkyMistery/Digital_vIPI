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
/// <para>Il JSON è corto di proposito: chiavi di una lettera e coordinate a <b>sei</b> decimali (circa 11 cm).
/// L'albero vero è 244 227 punti: scritti per esteso sarebbero decine di MB da serializzare a ogni accensione.</para>
/// </summary>
public static class MappaDelLab
{
    /// <summary>
    /// Quanti decimali di grado: 6 ≈ 11 cm in latitudine, un pixel allo zoom massimo della mappa (20). Erano 5 (1,1 m) con
    /// lo zoom fermo a 16; il committente ha chiesto di avvicinarsi di più ai layout degli scali (F3-bis, 23 settembre), e
    /// a 5 decimali i punti avrebbero saltato di un metro. Il sector è più preciso ancora: i millesimi di secondo sono ~3 cm.
    /// </summary>
    public const int Decimali = 6;

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
                flusso => Scrivi(flusso, id, strato?.Forme ?? [], lab.Colori, lab.Simboli),
                contentType: "application/json");
        });
    }

    /// <summary>Scrive uno strato nel formato corto. Pubblica perché la misura sull'albero vero la chiama da fuori.</summary>
    /// <param name="colori">I colori di Aurora (slice 4): con questi ogni forma porta <c>k</c> (la linea, <c>#RRGGBB</c>,
    /// null = non si disegna), <c>ka</c> (la sua opacità se non è piena), <c>g</c>/<c>ga</c> (il riempimento) e
    /// <c>s</c> (lo stile della linea dello schema, se non è continua). La pagina passa fra i colori del Lab e questi
    /// senza riprendere le coordinate.</param>
    /// <param name="simboli">I simboli del <c>.sym</c> (slice 4d): un punto che ne ha uno porta <c>y</c>, l'indice nell'elenco
    /// che la pagina riceve coi colori.</param>
    public static async Task Scrivi(Stream flusso, string id, IReadOnlyList<FormaDellaMappa> forme, ColoriDellaMappa? colori = null,
                                    SimboliDellaMappa? simboli = null)
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

            if (colori is not null)
            {
                var colore = colori.Di(forma);
                ScriviIlColore(json, "k", "ka", colore.Tratto, sempre: true);
                ScriviIlColore(json, "g", "ga", colore.Riempimento, sempre: false);
                if (colore.Tratteggio != 0)
                    json.WriteNumber("s", colore.Tratteggio);
            }

            if (simboli?.Di(forma) is { } simbolo)
                json.WriteNumber("y", simbolo);

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

            // La parte di ogni tratto (slice 6), dove non è semplicemente il suo numero: il poligono di un confine.
            if (forma.Parti is { } parti && !parti.Select((p, i) => p == i).All(u => u))
            {
                json.WriteStartArray("q");
                foreach (int parte in parti)
                    json.WriteNumberValue(parte);
                json.WriteEndArray();
            }

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

    /// <summary>Un colore come <c>#RRGGBB</c> più l'opacità a parte (fra 0 e 1) solo se non è piena.</summary>
    private static void ScriviIlColore(Utf8JsonWriter json, string chiave, string chiaveOpacita, System.Drawing.Color? colore, bool sempre)
    {
        if (colore is not { } c)
        {
            if (sempre)
                json.WriteNull(chiave);
            return;
        }

        json.WriteString(chiave, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
        if (c.A != 0xFF)
            json.WriteNumber(chiaveOpacita, Math.Round(c.A / 255.0, 3));
    }
}
