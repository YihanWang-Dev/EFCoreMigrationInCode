public static class StringExtensions
{

    public static (int? length, int? @decimal) GetColumnDataLength(this IProperty property)
    {
        string type = property.GetColumnType();
        var tokens = type.Split('(');
        if (tokens.Length > 1)
        {
            tokens = tokens[1]
                .Trim()
                .Trim(')')
                .Split(',');
            if (tokens.Length > 1)
            {
                var first = tokens[0].Trim();
                var second = tokens[1].Trim();
                if (int.TryParse(first, out var f1))
                {
                    if (int.TryParse(second, out var s1))
                    {
                        return (f1, s1);
                    }
                    return (f1, null);
                }
            }
            if (tokens.Length == 1)
            {
                if (int.TryParse(tokens[0], out var l))
                    return (l, null);
            }
        }
        return (null, null);
    }

    public static string GetColumnTypeForSql(this IProperty property) => property.GetColumnType().Split('(')[0].Trim();

    public static string[] GetOldNames(this IProperty property)
    {
        if (property.PropertyInfo == null)
        {
            return null;
        }
        var oa = property.PropertyInfo.GetCustomAttributes<OldNameAttribute>();

        return oa.Select(x => x.Name).ToArray();
    }

    public static string ToJoinString(this IEnumerable<string> list, string separator = ", ") => string.Join(separator, list);

    public static string ToJoinString(
        this IEnumerable<string> list,
        Func<string, string> escape,
        string separator = ", ") => string.Join(separator, list.Select(x => escape(x)));

    public static string Extract(
      this string text,
      string start,
      string end,
      StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string)null;
        if (string.IsNullOrWhiteSpace(start))
            throw new ArgumentNullException(nameof(start));
        if (string.IsNullOrWhiteSpace(end))
            throw new ArgumentNullException(nameof(end));
        int num = text.IndexOf(start, 0, comparison);
        if (num == -1)
            return (string)null;
        text = text.Substring(num + start.Length);
        int length = text.IndexOf(end, start.Length, comparison);
        return length == -1 ? "" : text.Substring(0, length);
    }

    public static bool StartsWithIgnoreCase(this string text, string test) => text.StartsWith(test, StringComparison.OrdinalIgnoreCase);

    public static bool StartsWithIgnoreCase(this string text, params string[] test) => ((IEnumerable<string>)test).Any<string>((Func<string, bool>)(t => text.StartsWith(t, StringComparison.OrdinalIgnoreCase)));

    public static bool EqualsIgnoreCase(this string text, string test)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.IsNullOrWhiteSpace(test);
        return !string.IsNullOrWhiteSpace(test) && text.Equals(test, StringComparison.OrdinalIgnoreCase);
    }

    public static bool EqualsIgnoreCase(this string text, params string[] test) => string.IsNullOrWhiteSpace(text) ? ((IEnumerable<string>)test).Any<string>((Func<string, bool>)(t => string.IsNullOrWhiteSpace(t))) : ((IEnumerable<string>)test).Any<string>((Func<string, bool>)(t => !string.IsNullOrWhiteSpace(t) && text.Equals(t, StringComparison.OrdinalIgnoreCase)));

    public static string Last(this string text, int charLength)
    {
        if (text == null)
            return (string)null;
        return text.Length <= charLength ? text : text.Substring(text.Length - charLength);
    }

    public static List<T> SplitAs<T>(this string ctIDs, Func<string, T> converter)
    {
        List<T> objList = new List<T>();
        if (string.IsNullOrWhiteSpace(ctIDs))
            return objList;
        converter = (Func<string, T>)(value => (T)Convert.ChangeType((object)value, typeof(T)));
        string str1 = ctIDs;
        char[] chArray = new char[2] { ',', ';' };
        foreach (string str2 in str1.Split(chArray))
        {
            if (!string.IsNullOrWhiteSpace(str2))
            {
                T obj = converter(str2);
                objList.Add(obj);
            }
        }
        return objList;
    }

    public static (string, string) ExtractTill(this string input, string separator)
    {
        int length = input.IndexOf(separator);
        return length == -1 ? (input, "") : (input.Substring(0, length), input.Substring(length + 1));
    }

    public static string SubstringTill(this string input, string separator)
    {
        bool flag = false;
        if (input.StartsWith("@"))
        {
            flag = true;
            input = input.Substring(1);
        }
        input = input.Split(separator.ToCharArray())[0];
        if (flag)
            input = "@" + input;
        return input;
    }

    public static string ToQuoted(this string input) => "\"" + input + "\"";

    public static string JoinText(this IEnumerable<string> list, string sep = ", ") => string.Join(sep, list);

}