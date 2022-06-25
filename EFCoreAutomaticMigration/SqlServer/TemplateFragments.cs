public struct TemplateFragments
{
    private List<TemplateQuery> fragments;
    private readonly string separator;
    private readonly string prefix;

    public TemplateFragments(string separator)
    {
        this.prefix = null;
        this.separator = separator;
        this.fragments = new List<TemplateQuery>();
    }

    public TemplateFragments(string separator, string prefix)
    {
        this.prefix = prefix;
        this.separator = separator;
        this.fragments = new List<TemplateQuery>();
    }

    public void Add(TemplateQuery fragment) => this.fragments.Add(fragment);

    public void Add(FormattableString fragment) => this.fragments.Add((TemplateQuery)fragment);

    public TemplateQuery ToSqlQuery() => TemplateQuery.Join(this.prefix, this.separator, (IEnumerable<TemplateQuery>)this.fragments);
}