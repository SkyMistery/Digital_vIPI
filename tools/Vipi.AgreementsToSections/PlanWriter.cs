using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.AgreementsToSections;

/// <summary>Quanto è stato scritto: il numero che il rapporto finale dice a voce.</summary>
public sealed record Scritte(int Agreements, int Sections, int Clauses, int Airports, int Deleted);

/// <summary>
/// Esegue il piano sull'archivio: crea le sezioni, riappende clausole e aeroporti, riempie i due capi, e
/// cancella i gusci che il piano ha assorbito o scartato.
///
/// <para>⚠️ <b>Le clausole non si riscrivono: cambiano padre.</b> Punti, livelli, parità, condizioni, faccetta
/// e velocità non vengono mai toccati, e nemmeno gli <b>id</b>. È la ragione per cui la rete di
/// caratterizzazione può ancora dire «la derivazione non è cambiata»: se quei campi passassero da qui, la sua
/// approvazione non proverebbe più niente.</para>
///
/// <para>⚠️ Le colonne nuove si scrivono in <b>SQL grezzo</b> come le vecchie si leggono, per la stessa ragione
/// al contrario: fra la migrazione additiva e quella finale lo schema è <b>misto</b>, e un <c>SaveChanges</c> di
/// EF pretenderebbe di scrivere anche le colonne che per lui esistono già e che qui non ci sono ancora — o di
/// non scrivere quelle vecchie, che invece sono ancora <c>NOT NULL</c>.</para>
///
/// <para>Tutto in <b>una transazione</b>: una conversione a metà è peggio di una non fatta, perché non si
/// distingue da una fatta.</para>
/// </summary>
public static class PlanWriter
{
    public static async Task<Scritte> ApplicaAsync(VipiDbContext db, ConversionPlan piano)
    {
        var sqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var ord = sqlite ? "\"Order\"" : "`Order`";

        await db.Database.OpenConnectionAsync();

        // ⚠️ Una seconda passata NON è innocua: leggerebbe le righe già convertite come se fossero ancora
        // vecchie, e rifonderebbe accordi già fusi mescolando i loro aeroporti. Il tool si ferma da sé —
        // «l'ho lanciato due volte» non deve poter essere un modo di rovinare l'archivio.
        if (await ContaAsync(db, "select count(*) from AgreementSections") > 0)
        {
            await db.Database.CloseConnectionAsync();
            throw new InvalidOperationException(
                "Ci sono già delle sezioni: la conversione è stata fatta. Rilanciarla mescolerebbe ciò che ha "
                + "già unito — riparti dal backup se qualcosa non va.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var sezioni = 0;
            var clausole = 0;
            var aeroporti = 0;

            foreach (var a in piano.Agreements)
            {
                // I due capi, in forma canonica: li ha già decisi il piano.
                await EseguiAsync(db, "update CoordinationAgreements set SideASectorId = @a, SideBSectorId = @b, "
                                      + "Note = Description where Id = @id",
                    ("@a", a.SideASectorId), ("@b", a.SideBSectorId), ("@id", a.KeepAgreementId));

                foreach (var s in a.Sections)
                {
                    await EseguiAsync(db,
                        $"insert into AgreementSections (AgreementId, Kind, Direction, Description, {ord}) "
                        + "values (@agr, @kind, @dir, @desc, @ord)",
                        ("@agr", a.KeepAgreementId), ("@kind", s.Kind.ToString()), ("@dir", s.Direction.ToString()),
                        ("@desc", (object?)s.Description ?? DBNull.Value), ("@ord", s.Order));

                    var sectionId = await UltimoIdAsync(db, sqlite);
                    sezioni++;

                    // Gli aeroporti seguono la sezione: vengono dai vecchi accordi che ci sono confluiti, e
                    // ognuno porta la sua riga — l'ordine è quello che il piano ha conservato.
                    var ordApt = 0;
                    foreach (var apt in s.Airports)
                    {
                        await EseguiAsync(db,
                            $"insert into AgreementAirports (AgreementId, SectionId, Icao, Name, {ord}) "
                            + "values (@agr, @sec, @icao, @name, @ord)",
                            ("@agr", a.KeepAgreementId), ("@sec", sectionId), ("@icao", apt.Icao),
                            ("@name", (object?)apt.Name ?? DBNull.Value), ("@ord", ++ordApt));
                        aeroporti++;
                    }

                    // ⚠️ Le clausole si SPOSTANO: stessa riga, stesso id, altro padre e altra posizione. Nessun
                    // dato editoriale passa di qui.
                    //
                    // ⚠️ E si sposta anche il VECCHIO AgreementId, che a questo punto dello schema misto esiste
                    // ancora e porta ancora il suo FK in cascade. Lasciarlo puntare al guscio assorbito
                    // significa che la cancellazione del guscio, più sotto, si porta via la clausola — con il
                    // SectionId già scritto giusto. Provato eseguendo su una copia: delle 60 clausole ne
                    // sopravvivevano 23, e nessun errore lo diceva.
                    foreach (var c in s.Clauses)
                    {
                        await EseguiAsync(db,
                            $"update AgreementClauses set AgreementId = @agr, SectionId = @sec, {ord} = @ord, "
                            + "VariantGroup = @grp, VariantDepth = @depth where Id = @id",
                            ("@agr", a.KeepAgreementId), ("@sec", sectionId), ("@ord", c.Order),
                            ("@grp", (object?)c.VariantGroup ?? DBNull.Value), ("@depth", c.VariantDepth),
                            ("@id", c.ClauseId));
                        clausole++;
                    }
                }
            }

            // Gli aeroporti VECCHI: erano appesi all'accordo, e le loro righe sono state riscritte sopra sotto
            // le sezioni. Quelle originali se ne vanno adesso — riconoscibili perché sono le sole rimaste senza
            // sezione.
            await EseguiAsync(db, "delete from AgreementAirports where SectionId is null");

            // I gusci: quelli assorbiti in una coppia e quelli scartati perché non avevano niente da salvare.
            // ⚠️ Vanno via DOPO che clausole e aeroporti hanno cambiato padre: cancellarli prima se li
            // porterebbe dietro in cascade, e la conversione perderebbe proprio ciò che doveva salvare.
            var daEliminare = piano.Agreements.SelectMany(a => a.AbsorbedAgreementIds)
                .Concat(piano.Discarded).Distinct().ToList();
            foreach (var id in daEliminare)
                await EseguiAsync(db, "delete from CoordinationAgreements where Id = @id", ("@id", id));

            await tx.CommitAsync();
            return new Scritte(piano.Agreements.Count, sezioni, clausole, aeroporti, daEliminare.Count);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> ContaAsync(VipiDbContext db, string sql)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>L'id appena inserito. Le due sintassi sono diverse, e sono le due che servono.</summary>
    private static async Task<int> UltimoIdAsync(VipiDbContext db, bool sqlite)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = sqlite ? "select last_insert_rowid()" : "select last_insert_id()";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task EseguiAsync(VipiDbContext db, string sql, params (string Nome, object Valore)[] parametri)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = sql;
        foreach (var (nome, valore) in parametri)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = nome;
            p.Value = valore;
            cmd.Parameters.Add(p);
        }
        await cmd.ExecuteNonQueryAsync();
    }
}
