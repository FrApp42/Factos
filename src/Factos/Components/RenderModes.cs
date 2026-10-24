using Microsoft.AspNetCore.Components.Web;

namespace Factos.Components;

public static class RenderModes
{
    /// Business pages load from the database on init; prerendering would do it twice
    /// and briefly show a form the user cannot use yet.
    public static readonly InteractiveServerRenderMode Server = new(prerender: false);
}
