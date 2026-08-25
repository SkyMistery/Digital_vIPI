using Microsoft.EntityFrameworkCore.Metadata;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Come si chiede a una proprietà del modello «in che tipo finisci nel database?».
///
/// <para>Esiste perché la risposta ha <b>due strade</b> e chi ne guarda una sola sbaglia in silenzio. La
/// conversione globale enum→stringa di <c>VipiDbContext.OnModelCreating</c> passa per
/// <c>SetProviderClrType</c>, che <see cref="IReadOnlyProperty.GetProviderClrType"/> riporta. Ma una proprietà
/// con un <c>HasConversion</c> <b>suo</b> — oggi <c>AuditLog.Action</c>, che si legge tollerante — porta il tipo
/// dentro il convertitore e lascia l'altro a <c>null</c>.</para>
///
/// <para>⚠️ Misurato il 25 agosto 2026: la sola aggiunta di un <c>HasConversion</c> ha fatto uscire quella
/// colonna da <b>due</b> regole del modello MySQL insieme — la lunghezza degli enum
/// (<see cref="MySqlStringLengths"/>, <c>varchar(32)</c> diventato <c>longtext</c>) e la collation
/// case-sensitive (<see cref="MySqlCollation"/>, sparita del tutto). Nessuna delle due aveva torto: guardavano
/// la strada che allora era l'unica. Da qui un posto solo, così la terza regola che nascerà non ripeterà
/// l'errore.</para>
/// </summary>
internal static class TipoColonna
{
    /// <summary>Tipo CLR con cui la proprietà arriva al database, per entrambe le strade. null = non dichiarato
    /// (si ricade sul tipo della proprietà).</summary>
    public static Type? Provider(IMutableProperty prop) =>
        prop.GetProviderClrType() ?? prop.GetValueConverter()?.ProviderClrType;
}
