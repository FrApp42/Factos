using Factos.Domain;

namespace Factos.Tests;

public class ThemeTests : ApiTestBase
{
    [Fact]
    public async Task A_user_gets_their_own_theme_else_the_default_and_the_login_page_its_own()
    {
        await WithDb(async db =>
        {
            Assert.Equal(Themes.Fallback, await Themes.ForUserAsync(db, Jane));   // nothing set anywhere

            await Themes.SetGlobalAsync(db, Themes.DefaultOption, "teal");
            await Themes.SetGlobalAsync(db, Themes.LoginOption, "emerald");
            Assert.Equal("teal", await Themes.ForUserAsync(db, Jane));
            Assert.Equal("emerald", await Themes.ForUserAsync(db, null));

            await Themes.SetUserAsync(db, Jane, "graphite");
            Assert.Equal("graphite", await Themes.ForUserAsync(db, Jane));
            Assert.Equal("teal", await Themes.ForUserAsync(db, Bob));

            await Themes.SetUserAsync(db, Jane, "");   // back to following the default
            Assert.Equal("teal", await Themes.ForUserAsync(db, Jane));

            await Themes.SetUserAsync(db, Jane, "not-a-theme");
            Assert.Equal(Themes.Fallback, await Themes.ForUserAsync(db, Jane));
        });

        var html = await Client.GetStringAsync("/Account/Login");
        Assert.Contains("<html lang=\"fr\" data-theme=\"emerald\">", html);
    }
}
