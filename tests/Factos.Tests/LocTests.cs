using System.Globalization;
using Factos.Components;

namespace Factos.Tests;

public class LocTests
{
    [Fact]
    public void French_is_the_key_and_English_comes_from_the_dictionary()
    {
        var old = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            Assert.Equal("Factures", Loc.T("Factures"));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal("Invoices", Loc.T("Factures"));
            Assert.Equal("Voir la facture X", Loc.T("Voir la facture X")); // missing key falls back
            Assert.Equal("View invoice INV-1", Loc.F("Voir la facture {0}", "INV-1"));
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }
}
