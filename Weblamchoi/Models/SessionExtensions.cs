namespace weblamchoi.Models
{
    public static class SessionExtensions
    {
        public static void SetDecimal(this ISession session, string key, decimal value)
        {
            session.SetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static decimal? GetDecimal(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
