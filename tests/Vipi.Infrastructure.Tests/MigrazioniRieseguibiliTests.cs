using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vipi.Infrastructure.MySqlMigrations;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// U-096 (revisione totale 3): su MariaDB una migrazione interrotta a metà deve potersi finire all'avvio dopo.
/// Qui si prova la SQL, senza un server; che MariaDB 11.4.10 la esegua due volte e arrivi allo stesso schema lo
/// prova la CI (<c>mariadb-schema</c>, «Migrazioni interrotte»).
/// </summary>
public class MigrazioniRieseguibiliTests
{
    [Theory]
    [InlineData("CREATE TABLE `A` (", "CREATE TABLE IF NOT EXISTS `A` (")]
    [InlineData("CREATE INDEX `IX_A` ON `A` (`X`);", "CREATE INDEX IF NOT EXISTS `IX_A` ON `A` (`X`);")]
    [InlineData("CREATE UNIQUE INDEX `IX_A` ON `A` (`X`);", "CREATE UNIQUE INDEX IF NOT EXISTS `IX_A` ON `A` (`X`);")]
    [InlineData("DROP TABLE `A`;", "DROP TABLE IF EXISTS `A`;")]
    [InlineData("ALTER TABLE `A` ADD `X` int NOT NULL DEFAULT 0;", "ALTER TABLE `A` ADD COLUMN IF NOT EXISTS `X` int NOT NULL DEFAULT 0;")]
    [InlineData("ALTER TABLE `A` DROP COLUMN `X`;", "ALTER TABLE IF EXISTS `A` DROP COLUMN IF EXISTS `X`;")]
    [InlineData("ALTER TABLE `A` DROP INDEX `IX_A`;", "ALTER TABLE IF EXISTS `A` DROP INDEX IF EXISTS `IX_A`;")]
    [InlineData("ALTER TABLE `A` DROP FOREIGN KEY `FK_A`;", "ALTER TABLE IF EXISTS `A` DROP FOREIGN KEY IF EXISTS `FK_A`;")]
    [InlineData("ALTER TABLE `A` ADD CONSTRAINT `FK_A` FOREIGN KEY (`B`) REFERENCES `B` (`Id`);",
        "ALTER TABLE `A` ADD CONSTRAINT `FK_A` FOREIGN KEY IF NOT EXISTS (`B`) REFERENCES `B` (`Id`);")]
    [InlineData("ALTER TABLE `A` ADD CONSTRAINT `AK_A` UNIQUE (`X`);", "ALTER TABLE `A` ADD CONSTRAINT `AK_A` UNIQUE IF NOT EXISTS (`X`);")]
    [InlineData("ALTER TABLE `A` RENAME COLUMN `X` TO `Y`;", "ALTER TABLE `A` RENAME COLUMN IF EXISTS `X` TO `Y`;")]
    [InlineData("ALTER TABLE `A` RENAME `B`;", "ALTER TABLE IF EXISTS `A` RENAME `B`;")]
    public void Ogni_forma_di_Pomelo_diventa_rieseguibile_una_volta_sola(string pomelo, string atteso)
    {
        var una = MigrazioniRieseguibili.RendiRieseguibile(pomelo);
        Assert.Equal(atteso, una);
        // Passarla di nuovo non raddoppia niente: «IF NOT EXISTS IF NOT EXISTS» è un errore di sintassi.
        Assert.Equal(atteso, MigrazioniRieseguibili.RendiRieseguibile(una));
    }

    [Theory]
    [InlineData("ALTER TABLE `A` MODIFY COLUMN `X` int NOT NULL;")]
    [InlineData("UPDATE DocumentSections SET IsHidden = 1 WHERE SectionKey = 'stars'")]
    // Una parola chiave dentro una stringa non è l'inizio di un'istruzione.
    [InlineData("UPDATE T SET Nota = 'CREATE TABLE `A` (' WHERE Id = 1")]
    public void Il_resto_non_si_tocca(string sql)
    {
        Assert.Same(sql, MigrazioniRieseguibili.RendiRieseguibile(sql));
    }

    /// <summary>
    /// Il turno si prende solo con un 1: 0 (attesa scaduta) e NULL (errore del server) fermano l'avvio, perché
    /// migrare senza turno è il guasto che il turno toglie. MySqlConnector risponde con un long.
    /// </summary>
    [Fact]
    public void Il_turno_delle_migrazioni_si_prende_solo_con_un_uno()
    {
        var attesa = TimeSpan.FromMinutes(5);
        TurnoDelleMigrazioni.Verifica(1L, attesa);
        TurnoDelleMigrazioni.Verifica(1, attesa);

        var scaduta = Assert.Throws<InvalidOperationException>(() => TurnoDelleMigrazioni.Verifica(0L, attesa));
        Assert.Contains("5 minuti", scaduta.Message);
        Assert.Throws<InvalidOperationException>(() => TurnoDelleMigrazioni.Verifica(null, attesa));
        Assert.Throws<InvalidOperationException>(() => TurnoDelleMigrazioni.Verifica(DBNull.Value, attesa));
    }

    /// <summary>
    /// 🔴 La guardia. Ogni istruzione di ogni migrazione MySQL, così come l'avvio la esegue, è rieseguibile: o
    /// passa dalle riscritture, o è in <see cref="MigrazioniRieseguibili.RieseguibiliPerNatura"/>. Una migrazione
    /// nuova con una forma che nessuno ha guardato (un <c>ADD PRIMARY KEY</c>, un <c>INSERT</c> di dati) fa
    /// diventare rosso questo test invece di bloccare l'avvio in produzione alla prima interruzione.
    /// </summary>
    [Fact]
    public void Nessuna_istruzione_delle_migrazioni_resta_non_rieseguibile()
    {
        using var db = new MySqlMigrationsDesignTimeFactory().CreateDbContext([]);
        Assert.IsType<MigrazioniRieseguibili>(db.GetService<IMigrationsSqlGenerator>());

        var sql = db.GetService<IMigrator>().GenerateScript();
        var istruzioni = Regex.Split(sql, @";\r?\n\r?\n")
            .Select(i => i.Trim().TrimStart('﻿').Trim())
            .Where(i => i.Length > 0)
            .ToList();
        Assert.True(istruzioni.Count > 300, $"lo script si è spezzato male: {istruzioni.Count} istruzioni");

        var sconosciute = istruzioni.Where(i => !MigrazioniRieseguibili.Riconosciuta(i))
            .Select(i => i.Split('\n')[0]).ToList();
        Assert.True(sconosciute.Count == 0,
            $"{sconosciute.Count} istruzioni che al secondo giro fallirebbero su MariaDB: aggiungere la forma in " +
            "MigrazioniRieseguibili.Riscritture, o scriverle rieseguibili a mano.\n  " + string.Join("\n  ", sconosciute));
    }
}
