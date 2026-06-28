using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta read-only verso l'elenco ACC navigabili, derivato dalle FIR presenti nel DB.
/// Sostituisce l'elenco hardcoded: con DB vuoto non mostra ACC inesistenti; appena si crea una FIR
/// compare la card relativa. Sincrona di proposito (tabella piccola) per non propagare async ai call-site.
/// </summary>
public interface IStationDirectory
{
    IReadOnlyList<AccInfo> ListAccs();
}
