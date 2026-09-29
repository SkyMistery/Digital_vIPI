using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>Scrive un <see cref="MessaggioCpdlc"/>: <c>Comando;Risposta;Gruppo;</c> e i campi dei valori com'erano (slice 11c).</summary>
public sealed class CpdlcSaver : IFileSaver<MessaggioCpdlc>
{
    public IReadOnlyList<string> Serialize(MessaggioCpdlc record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return [string.Join(";", new[] { record.Comando, record.Risposta, record.Gruppo }.Concat(record.Valori)) + ";"];
    }

    public string GetIdentifier(MessaggioCpdlc record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Comando;
    }
}

/// <summary>Scrive un <see cref="NomeDelGruppoCpdlc"/>: <c>GROUP.ID;NOME;</c> (slice 11c).</summary>
public sealed class CpdlcNamesSaver : IFileSaver<NomeDelGruppoCpdlc>
{
    public IReadOnlyList<string> Serialize(NomeDelGruppoCpdlc record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return [$"GROUP.{record.Gruppo};{record.Nome};"];
    }

    public string GetIdentifier(NomeDelGruppoCpdlc record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Gruppo;
    }
}
