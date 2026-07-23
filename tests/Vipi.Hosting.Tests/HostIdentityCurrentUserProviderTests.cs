using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Path di produzione (audit D1): <see cref="HostIdentityCurrentUserProvider"/> proietta il ClaimsPrincipal del
/// sito host sul modello neutro. Copre i formati di claim staff (semplice, array JSON, oggetti) e i casi vuoti.
/// </summary>
public class HostIdentityCurrentUserProviderTests
{
    private static HostIdentityCurrentUserProvider Build(ClaimsPrincipal? principal, HostIdentityOptions? opt = null)
    {
        var ctx = new DefaultHttpContext();
        if (principal is not null) ctx.User = principal;
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return new HostIdentityCurrentUserProvider(accessor, Options.Create(opt ?? new HostIdentityOptions()));
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    [Fact]
    public void Anonymous_principal_returns_null()
    {
        Assert.Null(Build(new ClaimsPrincipal(new ClaimsIdentity())).Get());   // non autenticato
        Assert.Null(Build(principal: null).Get());                             // nessun HttpContext.User
    }

    [Fact]
    public void Missing_or_invalid_userid_returns_null()
    {
        Assert.Null(Build(Authenticated(new Claim("name", "Tizio"))).Get());        // manca id
        Assert.Null(Build(Authenticated(new Claim("id", "abc"))).Get());            // id non numerico
        Assert.Null(Build(Authenticated(new Claim("id", "0"))).Get());              // id non positivo
    }

    [Fact]
    public void Maps_userid_name_acc_and_simple_staff_positions()
    {
        var user = Build(Authenticated(
            new Claim("id", "704798"),
            new Claim("name", "Mario Rossi"),
            new Claim("centerId", "LIRR"),
            new Claim("userStaffPositions", "IT-DIR"),
            new Claim("userStaffPositions", "LIRR-CH"))).Get();

        Assert.NotNull(user);
        Assert.Equal(704798, user!.UserId);
        Assert.Equal("Mario Rossi", user.Name);
        Assert.Equal("LIRR", user.Acc);
        Assert.Contains("IT-DIR", user.StaffPositions);
        Assert.Contains("LIRR-CH", user.StaffPositions);
        Assert.True(user.CanEdit);
    }

    [Fact]
    public void Parses_json_array_staff_positions_claim()
    {
        var user = Build(Authenticated(
            new Claim("id", "1"),
            new Claim("userStaffPositions", "[\"IT-WM\",\"IT-AOC\"]"))).Get();

        Assert.NotNull(user);
        Assert.Contains("IT-WM", user!.StaffPositions);
        Assert.Contains("IT-AOC", user.StaffPositions);
    }

    [Fact]
    public void Parses_json_object_staff_positions_by_id_field()
    {
        var user = Build(Authenticated(
            new Claim("id", "1"),
            new Claim("userStaffPositions", "[{\"id\":\"IT-DIR\"},{\"connectAs\":\"LIRR_CTR\"}]"))).Get();

        Assert.NotNull(user);
        Assert.Contains("IT-DIR", user!.StaffPositions);
        Assert.Contains("LIRR_CTR", user.StaffPositions);
    }

    [Fact]
    public void No_staff_positions_means_cannot_edit()
    {
        var user = Build(Authenticated(new Claim("id", "1"), new Claim("name", "Pilota"))).Get();
        Assert.NotNull(user);
        Assert.Empty(user!.StaffPositions);
        Assert.False(user.CanEdit);
    }

    [Fact]
    public void Falls_back_to_sub_claim_for_userid()
    {
        var user = Build(Authenticated(new Claim("sub", "555"))).Get();
        Assert.NotNull(user);
        Assert.Equal(555, user!.UserId);
    }
}
