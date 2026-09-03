using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Chi decide la lingua di una pagina che mostra <b>due documenti</b> (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §3, correzione della supervisione del 3 settembre 2026).
///
/// <para>
/// 🔴 <c>ReadingLanguageContext.Fissa</c> non ha un blocco che lo chiuda: vale per il <b>resto della
/// richiesta</b>. La sua stessa documentazione dice che regge perché le pagine documentali sono SSR statiche
/// e mostrano <b>un documento sola</b> — e l'unione ha rotto quella premessa senza che nessuno rileggesse
/// quella riga. Con N membri, <c>Prepara</c> viene chiamato N volte e l'ULTIMO membro a lingua bloccata
/// deciderebbe la lingua delle etichette e della prosa generata di tutta la pagina, ospite compreso, in base
/// all'ORDINE DI CARICAMENTO.
/// </para>
///
/// <para>
/// ⚠️ Il confine è fra <b>pagina</b> e <b>contenuto</b>: il contenuto di un membro resta nella sua lingua
/// perché traduzione, titoli di catalogo e derivate ricevono il codice come <b>argomento</b>. Quel che il
/// membro non impone è la lingua della pagina, che nell'unione è dell'ospite.
/// </para>
/// </summary>
public class LinguaDellaPaginaUnitaTests
{
    [Fact]
    public void Un_documento_SOLO_e_bloccato_impone_la_sua_lingua_alla_pagina()
    {
        var ctx = new ReadingLanguageContext();

        var lettore = LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.En, Language.It);

        Assert.Equal("en", lettore);
        Assert.Equal("en", ctx.Fissata);
    }

    [Fact]
    public void Un_MEMBRO_bloccato_NON_impone_la_sua_lingua_alla_pagina()
    {
        var ctx = new ReadingLanguageContext();

        var lettore = LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.En, Language.It,
                                                 fissaLaPagina: false);

        // ⚠️ La lingua del SUO contenuto resta la sua: e' il valore restituito che la porta.
        Assert.Equal("en", lettore);
        // ⚠️ Ma la pagina non e' stata toccata.
        Assert.Null(ctx.Fissata);
    }

    /// <summary>
    /// 🔴 Il caso che si vedrebbe a schermo: l'ospite è italiano e non bloccato, il membro è inglese e
    /// bloccato. Prima di questa correzione la pagina intera — titoli, intestazioni di tabella, prosa
    /// derivata dell'OSPITE — finiva in inglese, e nessun errore lo diceva.
    /// </summary>
    [Fact]
    public void Ospite_italiano_e_membro_inglese_bloccato_la_pagina_resta_dell_ospite()
    {
        var ctx = new ReadingLanguageContext();

        var ospite = LinguaDelDocumento.Prepara(ctx, bloccato: false, Language.It, Language.It);
        var membro = LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.En, Language.It,
                                                fissaLaPagina: false);

        // ⚠️ L'ospite non e' bloccato, quindi si legge nella lingua di CHI GUARDA — non in quella in cui
        // e' scritto. Confrontarlo con "it" sarebbe stato un test che passa solo sulle macchine italiane.
        Assert.Equal(ctx.Corrente, ospite);
        Assert.Equal("en", membro);
        // Ed e' questo il punto: il membro bloccato non ha spostato la pagina.
        Assert.Null(ctx.Fissata);
    }

    /// <summary>
    /// ⚠️ E il verso giusto continua a valere: se è l'OSPITE a essere bloccato, la pagina lo segue —
    /// anche quando dopo di lui si carica un membro con un'altra lingua.
    /// </summary>
    [Fact]
    public void Se_e_l_OSPITE_a_essere_bloccato_il_membro_non_glielo_porta_via()
    {
        var ctx = new ReadingLanguageContext();

        LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.En, Language.It);
        LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.It, Language.It, fissaLaPagina: false);

        Assert.Equal("en", ctx.Fissata);
    }
}
