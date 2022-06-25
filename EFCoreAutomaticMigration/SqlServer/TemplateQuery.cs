public struct TemplateQuery
{
    internal List<(string literal, bool hasArgument, object argument)> fragments;
    private static object[] Empty = new object[0];

    public static implicit operator TemplateQuery(FormattableString sql) => new TemplateQuery(sql);

    public TemplateQuery(FormattableString sql)
      : this(sql.Format, sql.GetArguments())
    {
    }

    public static TemplateQuery Join(
      string prefix,
      string separator,
      IEnumerable<TemplateQuery> fragments)
    {
        TemplateQuery templateQuery = prefix != null ? TemplateQuery.FromString(prefix) : TemplateQuery.New();
        IEnumerator<TemplateQuery> enumerator = fragments.GetEnumerator();
        if (enumerator.MoveNext())
            templateQuery.fragments.AddRange((IEnumerable<(string, bool, object)>)enumerator.Current.fragments);
        while (enumerator.MoveNext())
        {
            templateQuery.fragments.Add((separator, false, (object)null));
            templateQuery.fragments.AddRange((IEnumerable<(string, bool, object)>)enumerator.Current.fragments);
        }
        return templateQuery;
    }

    public static TemplateQuery Join(
      string separator,
      IEnumerable<TemplateQuery> fragments)
    {
        return TemplateQuery.Join((string)null, separator, fragments);
    }

    public static TemplateQuery Join(
      string prefix,
      string separator,
      params FormattableString[] fragments)
    {
        return TemplateQuery.Join(prefix, separator, ((IEnumerable<FormattableString>)fragments).Select<FormattableString, TemplateQuery>((Func<FormattableString, TemplateQuery>)(x => TemplateQuery.New(x))));
    }

    public static TemplateQuery Join(
      string separator,
      params FormattableString[] fragments)
    {
        return TemplateQuery.Join((string)null, separator, fragments);
    }

    private TemplateQuery(string text, object[] args)
    {
        this.fragments = new List<(string, bool, object)>();
        if (string.IsNullOrWhiteSpace(text))
            return;
        for (int index = 0; index < args.Length; ++index)
        {
            string str1 = string.Format("{{{0}}}", (object)index);
            int length = text.IndexOf(str1);
            string str2 = text.Substring(0, length);
            text = text.Substring(length + str1.Length);
            object obj = args[index];
            this.fragments.Add((str2, false, (object)null));
            if (obj is Literal literal)
                this.fragments.Add((literal.Value, false, (object)null));
            else if (obj is TemplateQuery templateQuery)
                this.fragments.AddRange((IEnumerable<(string, bool, object)>)templateQuery.fragments);
            else if (obj is TemplateFragments templateFragments)
                this.fragments.AddRange((IEnumerable<(string, bool, object)>)templateFragments.ToSqlQuery().fragments);
            else if (!(obj is string) && obj is IEnumerable enumerable)
            {
                IEnumerator enumerator = enumerable.GetEnumerator();
                if (enumerator.MoveNext())
                    this.fragments.Add(((string)null, true, enumerator.Current));
                while (enumerator.MoveNext())
                {
                    this.fragments.Add((",", false, (object)null));
                    this.fragments.Add(((string)null, true, enumerator.Current));
                }
            }
            else
                this.fragments.Add(((string)null, true, args[index]));
        }
        this.fragments.Add((text, false, (object)null));
    }

    public FormattableString ToFormattableString() => (FormattableString)new SqlQueryFormattable(this);

    public static TemplateQuery operator +(TemplateQuery first, FormattableString sql)
    {
        TemplateQuery templateQuery = new TemplateQuery(sql);
        templateQuery.fragments.InsertRange(0, (IEnumerable<(string, bool, object)>)first.fragments);
        return templateQuery;
    }

    public static TemplateQuery operator +(TemplateQuery first, TemplateQuery r)
    {
        r.fragments.InsertRange(0, (IEnumerable<(string, bool, object)>)first.fragments);
        return r;
    }

    public static TemplateQuery New() => new TemplateQuery(FormattableStringFactory.Create(""));

    public static TemplateQuery Literal(string text) => new TemplateQuery(text, TemplateQuery.Empty);

    public static TemplateQuery New(params FormattableString[] sql)
    {
        if (sql.Length == 0)
            throw new ArgumentException("Atleast one query must be specified");
        TemplateQuery templateQuery = TemplateQuery.New();
        foreach (FormattableString formattableString in sql)
            templateQuery += formattableString;
        return templateQuery;
    }

    public override string ToString()
    {
        int ix = 0;
        return string.Join("", this.fragments.Select<(string, bool, object), string>((Func<(string, bool, object), int, string>)((x, i) => !x.Item2 ? x.Item1 : string.Format("{{{0}}}", (object)ix++))));
    }

    internal static TemplateQuery FromString(string format, params object[] parameters) => new TemplateQuery(format, parameters);

    public string Text
    {
        get
        {
            int ix = 0;
            return string.Join("", this.fragments.Select<(string, bool, object), string>((Func<(string, bool, object), int, string>)((x, i) => !x.Item2 ? x.Item1 : string.Format("@p{0}", (object)ix++))));
        }
    }

    public KeyValuePair<string, object>[] Values
    {
        get
        {
            int ix = 0;
            return this.fragments.Where<(string, bool, object)>((Func<(string, bool, object), bool>)(x => x.Item2)).Select<(string, bool, object), KeyValuePair<string, object>>((Func<(string, bool, object), KeyValuePair<string, object>>)(x => new KeyValuePair<string, object>(string.Format("p{0}", (object)ix++), x.Item2))).ToArray<KeyValuePair<string, object>>();
        }
    }
}

public struct Literal
{
    public readonly string Value;

    public static Literal DoubleQuoted(string text) => new Literal("\"" + text + "\"");

    public static Literal SquareBrackets(string text) => new Literal("[" + text + "]");

    public Literal(string value) => this.Value = value;

    public override string ToString() => this.Value;
}

public class SqlQueryFormattable : FormattableString
{
    private object[] values;

    public SqlQueryFormattable(TemplateQuery query)
    {
        this.Format = query.ToString();
        this.values = query.fragments.Where<(string, bool, object)>((Func<(string, bool, object), bool>)(x => x.Item2)).Select<(string, bool, object), object>((Func<(string, bool, object), object>)(x => x.Item2)).ToArray<object>();
    }

    public override int ArgumentCount => this.values.Length;

    public override string Format { get; }

    public override object GetArgument(int index) => this.values[index];

    public override object[] GetArguments() => this.values;

    public override string ToString(IFormatProvider formatProvider) => string.Format(formatProvider, this.Format, this.GetArguments());
}