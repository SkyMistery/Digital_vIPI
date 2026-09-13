using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La pagina AoR 3D a schermo pieno (<c>/services/vsop/aor3d/{kind}/{key}</c>) la apre solo l'Editor.
///
/// <para>🔴 <b>Perché (T-061, revisione del 13 settembre 2026).</b> Non ha ingressi nell'interfaccia dal 31
/// luglio, ma la rotta rispondeva a chiunque, e con la copia di <b>lavoro</b>: <c>vloa/{docId}</c> con id
/// enumerabili mostrava l'AoR anche di documenti nascosti o mai pubblicati. Chi ci arriva è chi la sta
/// rilavorando, cioè un Editor.</para>
///
/// <para>⚠️ Il rifiuto arriva <b>prima delle letture</b>: i servizi qui sono finti che sollevano a ogni chiamata.</para>
/// </summary>
public class PaginaAor3dTests : TestContext
{
    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.DivisionStaff)]
    public void Sotto_l_editor_la_pagina_dice_di_no_e_non_legge_niente(VipiRole livello)
    {
        Predisponi(livello);

        var cut = RenderComponent<Aor3dFullPage>(p => p.Add(x => x.Kind, "vloa").Add(x => x.Key, "42"));

        Assert.Contains("Common_AccessReserved", cut.Markup);
    }

    private void Predisponi(VipiRole livello)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaviNude());
        Services.AddSingleton(new EnglishStrings());
        Services.AddSingleton<IEditAuthorizationService>(new Authz(livello));
        Services.AddSingleton(Lancia<IAccDocumentService>());
        Services.AddSingleton(Lancia<IAccViewDerivationService>());
        Services.AddSingleton(Lancia<IAppDocumentService>());
        Services.AddSingleton(Lancia<IVloaDerivationService>());
    }

    private static T Lancia<T>() where T : class => DispatchProxy.Create<T, Solleva>();

    /// <summary>Qualunque chiamata solleva: se la pagina legge prima di rifiutare, il render cade.</summary>
    public class Solleva : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"letto prima del cancello: {targetMethod?.Name}");
    }

    private sealed class Authz(VipiRole livello) : IEditAuthorizationService
    {
        public VipiRole Role => livello;
        public bool IsAdmin => livello >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "Chi Prova";
    }

    private sealed class ChiaviNude : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }
}
