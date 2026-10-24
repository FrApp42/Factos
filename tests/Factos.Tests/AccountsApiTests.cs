using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Factos.Tests;

/// The Accounts endpoints over real HTTP, checked against the JSON InvoiceShelf returns.
public class AccountsApiTests : ApiTestBase
{
    [Fact]
    public async Task Ping_identifies_an_invoiceshelf_server()
    {
        var json = await GetJson("/api/ping");
        Assert.Equal("invoiceshelf-self-hosted", json.GetProperty("success").GetString());
    }

    [Fact]
    public async Task Login_validates_every_field_with_laravels_messages()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);

        var json = await Json(response);
        Assert.Equal("The username field is required. (and 2 more errors)", json.GetProperty("message").GetString());
        Assert.Equal("The device name field is required.", json.GetProperty("errors").GetProperty("device_name")[0].GetString());
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password_on_the_email_key()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "jane@example.com", password = "nope", device_name = "phone" });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);

        var json = await Json(response);
        Assert.Equal("The provided credentials are incorrect.", json.GetProperty("message").GetString());
        Assert.Equal("The provided credentials are incorrect.", json.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Login_issues_a_sanctum_token_that_authenticates_and_can_be_revoked()
    {
        var token = await Login("JANE@example.com"); // case-insensitive, like LOWER(email) upstream
        Assert.Matches(@"^\d+\|[A-Za-z0-9]{40}[0-9a-f]{8}$", token);

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal("true", await Client.GetStringAsync("/api/v1/auth/check"));

        var tokens = await GetJson("/api/v1/auth/tokens");
        Assert.True(tokens.GetProperty("data")[0].GetProperty("current").GetBoolean());

        var logout = await Client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.True((await Json(logout)).GetProperty("success").GetBoolean());

        var after = await Client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal("Unauthenticated.", (await Json(after)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Me_matches_upstreams_user_resource()
    {
        await SignIn(company: CompanyA);

        var user = (await GetJson("/api/v1/me")).GetProperty("data");

        Assert.Equal(
            ["id", "name", "email", "phone", "role", "contact_name", "company_name", "website", "enable_portal",
             "currency_id", "facebook_id", "google_id", "github_id", "created_at", "updated_at", "avatar",
             "is_owner", "is_super_admin", "roles", "formatted_created_at", "companies"],
            user.EnumerateObject().Select(p => p.Name));
        Assert.Equal("2026-03-04T10:20:30.000000Z", user.GetProperty("created_at").GetString());
        Assert.Equal("04 Mar 2026", user.GetProperty("formatted_created_at").GetString());
        Assert.True(user.GetProperty("is_owner").GetBoolean());
        Assert.Equal(0, user.GetProperty("avatar").GetInt32());

        var role = user.GetProperty("roles")[0];
        Assert.Equal("owner", role.GetProperty("name").GetString());
        Assert.Equal("user", role.GetProperty("pivot").GetProperty("entity_type").GetString());

        var companies = user.GetProperty("companies");
        Assert.Equal(2, companies.GetArrayLength());
        Assert.Equal("Owner", companies[0].GetProperty("user_role").GetString());
        Assert.Equal(JsonValueKind.Null, companies[1].GetProperty("user_role").ValueKind);
        Assert.False(companies[0].TryGetProperty("address", out _)); // when() keys are omitted, not null
    }

    [Fact]
    public async Task Company_header_falls_back_to_the_first_company_when_not_a_member()
    {
        await SignIn(company: 999);
        Assert.True((await GetJson("/api/v1/me")).GetProperty("data").GetProperty("is_owner").GetBoolean());

        await SignIn(company: CompanyB);
        Assert.False((await GetJson("/api/v1/me")).GetProperty("data").GetProperty("is_owner").GetBoolean());
    }

    [Fact]
    public async Task Revoking_an_unknown_token_is_a_laravel_404()
    {
        await SignIn();

        var response = await Client.DeleteAsync("/api/v1/auth/tokens/424242");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(@"No query results for model [Laravel\Sanctum\PersonalAccessToken].",
            (await Json(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Unknown_api_routes_answer_in_json()
    {
        var response = await Client.GetAsync("/api/v1/nope");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("The route api/v1/nope could not be found.", (await Json(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Updating_the_profile_validates_like_profile_request()
    {
        await SignIn();

        var empty = await Client.PutAsJsonAsync("/api/v1/me", new { });
        Assert.Equal((HttpStatusCode)422, empty.StatusCode);
        Assert.Equal("The name field is required. (and 1 more error)", (await Json(empty)).GetProperty("message").GetString());

        var invalid = await Client.PutAsJsonAsync("/api/v1/me", new { name = "Jane", email = "not-an-email", password = "short" });
        var errors = (await Json(invalid)).GetProperty("errors");
        Assert.Equal("The password field must be at least 8 characters.", errors.GetProperty("password")[0].GetString());
        Assert.Equal("The email must be a valid email address.", errors.GetProperty("email")[0].GetString());

        var taken = await Client.PutAsJsonAsync("/api/v1/me", new { name = "Jane", email = "bob@example.com" });
        Assert.Equal("The email has already been taken.", (await Json(taken)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Updating_the_profile_changes_name_email_and_password()
    {
        await SignIn();

        var response = await Client.PutAsJsonAsync("/api/v1/me",
            new { name = "Jane Doe", email = "jane.doe@example.com", password = "another-pass" });
        response.EnsureSuccessStatusCode();
        var user = (await Json(response)).GetProperty("data");
        Assert.Equal("Jane Doe", user.GetProperty("name").GetString());
        Assert.Equal("jane.doe@example.com", user.GetProperty("email").GetString());

        Assert.NotEmpty(await Login("jane.doe@example.com", "another-pass"));
    }

    [Fact]
    public async Task User_settings_round_trip_and_refuse_private_keys()
    {
        await SignIn();

        // An empty PHP collection serialises as a list.
        Assert.Equal(JsonValueKind.Array, (await GetJson("/api/v1/me/settings?settings[]=language")).ValueKind);

        var missing = await Client.GetAsync("/api/v1/me/settings");
        Assert.Equal("The settings field is required.", (await Json(missing)).GetProperty("message").GetString());

        (await Client.PutAsJsonAsync("/api/v1/me/settings", new { settings = new { language = "fr", rows = 25 } })).EnsureSuccessStatusCode();
        var settings = await GetJson("/api/v1/me/settings?settings[]=language&settings[]=rows&settings[]=absent");
        Assert.Equal("fr", settings.GetProperty("language").GetString());
        Assert.Equal("25", settings.GetProperty("rows").GetString()); // stored as text, read back as a string
        Assert.False(settings.TryGetProperty("absent", out _));

        var refused = await Client.PutAsJsonAsync("/api/v1/me/settings",
            new { settings = new Dictionary<string, object> { ["module.x"] = 1, ["company_mail_host"] = "h" } });
        Assert.Equal((HttpStatusCode)422, refused.StatusCode);
        Assert.Equal("These settings have their own page and cannot be changed here: module.x, company_mail_host.",
            (await Json(refused)).GetProperty("message").GetString());
    }
}
