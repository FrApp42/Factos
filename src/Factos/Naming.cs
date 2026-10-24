namespace Factos;

public static class Naming
{
    /// "CustomerName" -> "customer_name", "AddressStreet1" -> "address_street_1".
    /// Laravel's casing for both JSON keys and columns.
    public static string ToSnakeCase(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var boundary = i > 0 && (char.IsUpper(c) || (char.IsDigit(c) && char.IsLetter(value[i - 1])));
            if (boundary)
                result.Append('_');
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}
