public class PostGreSqlMigration : ModelMigrationBase
{
    public PostGreSqlMigration(DbContext context) : base(context)
    {
    }

    public override string Escape(string name) => $"\"{name.Trim('\"')}\"";

    protected override void AddColumn(DbColumnInfo property)
    {
        Run($"ALTER TABLE {property.Table.EscapedNameWithSchema} ADD " + ToColumn(property));
    }

    protected override void CreateIndex(SqlIndexEx index)
    {
        var name = index.Name;
        var tableName = GetTableNameWithSchema(index.DeclaringEntityType);
        var columns = string.Join(", ", index.Properties.Select(x => Escape(x.ColumnName())));
        string filter = index.Filter == null ? "" : " WHERE " + index.Filter;
        Run($"CREATE INDEX {name} ON {tableName} ({ columns }) {filter}");
    }

    protected override void DropIndex(SqlIndexEx index)
    {
        Run(TemplateQuery.New($"DROP INDEX IF EXISTS {Literal.DoubleQuoted(index.Name)}"));
    }

    protected override void CreateTable(DbTableInfo entity, List<DbColumnInfo> pkeys)
    {
        var tableName = entity.EscapedNameWithSchema;

        string createTable = $" CREATE TABLE {tableName} ({ string.Join(",", pkeys.Select(c => ToColumn(c))) }, " +
             $"CONSTRAINT {entity.TableName}_pkey PRIMARY KEY( { string.Join(", ", pkeys.Select(x => x.EscapedColumnName)) } ))";

        Run(createTable);
    }

    protected override string GetTableNameWithSchema(IEntityType entity)
    {
        return Escape(entity.GetSchemaOrDefault()) + "." + Escape(entity.GetTableName());
    }

    private static string[] textTypes = new[] { "character varying", "varchar" };

    protected override bool IsText(string n) => textTypes.Any(a => a.Equals(n, StringComparison.OrdinalIgnoreCase));

    protected override bool IsDecimal(string n) => n.Equals("numeric", StringComparison.OrdinalIgnoreCase);

    public override string LoadTableColumns() => Scripts.SqlServerGetSchema;

    protected override void RenameColumn(DbColumnInfo property, string postFix)
    {
        var table = property.Table.EscapedNameWithSchema;
        var name = property.ColumnName;
        var newName = name + postFix;
        Run($"ALTER TABLE {table} RENAME {Escape(name)} TO {Escape(newName)}");
    }

    protected override string ToColumn(DbColumnInfo c)
    {
        var name = new StringBuilder();
        name.Append($"{c.EscapedColumnName} {c.DataType}");

        if (c.DataLength != null)
        {
            if (c.DataLength > 0 && c.DataLength < int.MaxValue)
            {
                name.Append($"({ c.DataLength })");
            }
            else
            {
                name.Append("(MAX)");
            }
        }
        if (c.Precision != null)
        {
            var np = 18;
            var nps = 2;

            name.Append($"({ np },{ nps })");
        }
        if (!c.IsKey)
        {
            // lets allow nullable to every field...
            if (!c.IsNullable)
            {
                name.Append(" NOT NULL ");
            }
        }

        if (c.IsIdentity)
        {
            name.Append(" GENERATED ALWAYS AS IDENTITY ");
        }

        if (!string.IsNullOrWhiteSpace(c.DefaultValue))
        {
            name.Append(" DEFAULT " + c.DefaultValue);
        }
        return name.ToString();

    }

    public override string LoadIndexes(IEntityType entity) => Scripts.SqlServerGetIndexes;

    protected override bool HasAnyRows(DbTableInfo table)
    {
        var sql = $"SELECT 1 FROM {table.EscapedNameWithSchema} LIMIT 1";
        var cmd = CreateCommand(sql);
        var i = cmd.ExecuteScalar();
        if (i == null)
            return false;
        return true;
    }

    protected override bool ModelExists(string tableName, string model) => false;
}