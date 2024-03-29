﻿

public class MigrationResult
{
    private readonly List<DbTableInfo> tables;

    public MigrationResult(List<DbTableInfo> tables)
    {
        this.tables = tables;
    }

    public IReadOnlyCollection<DbTableInfo> Modifications => tables;

    public string Log
    {
        get
        {
            StringBuilder sb = new StringBuilder();
            foreach (var m in tables)
            {
                sb.AppendLine($"Table {m.TableName} modified.");
                foreach (var c in m.columnsAdded)
                {
                    sb.AppendLine($"\tColumn: {c.ColumnName} added.");
                }
                foreach (var c in m.columnsRenamed)
                {
                    sb.AppendLine($"\tColumn: {c.from.ColumnName} {c.from} renamed to {c.to}.");
                }
                foreach (var i in m.indexedUpdated)
                {
                    if (i.Dropped)
                    {
                        sb.AppendLine($"\tIndex: {i.Index.Name} dropped.");
                        continue;
                    }
                    sb.AppendLine($"\t{i.Index.Name} created.");
                }
            }
            return sb.ToString();
        }
    }
}

public class MigrationFailedException : Exception
{
    public MigrationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {

    }
}


public delegate void ColumnChangedDelegate(ModelMigrationBase migration,
    Column existing,
    DbColumnInfo currentColumn,
    string tempName);


public abstract class ModelMigrationBase
{

    internal string MigrationsTable = "__AutomaticMigrations";

    protected internal readonly DbContext context;
    private readonly Columns columns;
    protected DbTransaction? Transaction { get; private set; }

    public MigrationEventList handler = new MigrationEventList();

    public ColumnChangedDelegate? onColumnChange = null;

    // private bool preventRename = false;


    public ModelMigrationBase(DbContext context)
    {
        this.context = context;
        this.columns = new Columns(this);
    }

    public ModelMigrationBase AddEvent<T>(MigrationEvents<T> events)
    {
        handler.Add(events);
        return this;
    }

    public abstract string LoadTableColumns();

    protected abstract bool IsText(string n);

    protected abstract bool IsDecimal(string n);

    public abstract string Escape(string name);


    protected abstract string GetTableNameWithSchema(IEntityType entity);



    private List<IEntityType> GetEntityTypes()
    {
        var r = new List<IEntityType>();
        var all = context.Model.GetEntityTypes();
        var pending = new List<IEntityType>();
        var owned = new List<IEntityType>();
        foreach (var entity in all)
        {
            if (entity.BaseType != null)
            {
                pending.Add(entity);
                continue;
            }
            if (entity.ClrType.GetCustomAttribute<OwnedAttribute>() != null)
            {
                owned.Add(entity);
                continue;
            }
            r.Add(entity);
        }

        r.AddRange(pending);
        r.AddRange(owned);
        return r;
    }

    public bool TableExists(string tableName, string? schemaName = null)
    {
        var infoSchema = this.LoadTableColumns();
        using (var s = Read(infoSchema, new Dictionary<string, object> {
                        { "@TableName", tableName } ,
                        { "@SchemaName", schemaName ?? context.Model.GetDefaultSchema() }
                    }))
        {
            return s.Read();
        }
    }

    protected abstract bool ModelExists(string tableName, string model);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="preventChangeColumn">Set it to false for development</param>
    /// <returns></returns>
    public MigrationResult Migrate(bool preventChangeColumn = true)
    {
        if (preventChangeColumn)
        {
            this.onColumnChange ??= (m, e, p, t) => throw new NotSupportedException($"Changing column {e.ColumnName}{e} to {p.ColumnName}{p} not supported ");
        }
        var entities = GetEntityTypes();
        List<DbTableInfo> modifications = new List<DbTableInfo>();
        try
        {
            context.Database.OpenConnection();

            using (var tx = context.Database.GetDbConnection().BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                this.Transaction = tx;

                if (!string.IsNullOrEmpty(MigrationsTable))
                {
                    var name = TemplateQuery.Literal(MigrationsTable);

                    var rm = context.Database.GenerateCreateScript();

                    if (ModelExists(MigrationsTable, rm))
                    {
                        return new MigrationResult(modifications) { };
                    }
                }


                foreach (var entity in entities)
                {
                    if (entity.ClrType.GetCustomAttribute<IgnoreMigrationAttribute>() != null)
                        continue;

                    var table = new DbTableInfo(entity, Escape);
                    modifications.Add(table);
                    MigrateEntity(table);

                }
                foreach (var entity in entities)
                {
                    if (entity.ClrType.GetCustomAttribute<IgnoreMigrationAttribute>() != null)
                        continue;

                    var table = modifications.FirstOrDefault(x => x.EntityType == entity);
                    if (table == null)
                    {
                        table = new DbTableInfo(entity, Escape);
                        modifications.Add(table);
                    }
                    PostMigrateEntity(table);
                }
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            throw new MigrationFailedException(ex.Message + "\r\n" + new MigrationResult(modifications).Log, ex);
        }
        finally
        {
            context.Database.CloseConnection();
        }
        return new MigrationResult(modifications);
    }

    public virtual DbCommand CreateCommand(string command, IEnumerable<KeyValuePair<string, object>>? plist = null)
    {
        var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = command;
        cmd.Transaction = Transaction;
        if (plist != null)
        {
            foreach (var p in plist)
            {
                var px = cmd.CreateParameter();
                px.ParameterName = p.Key;
                px.Value = p.Value;
                cmd.Parameters.Add(px);
            }
        }
        return cmd;
    }

    public SqlRowSet Read(TemplateQuery query)
    {
        var cmd = CreateCommand(query.Text, query.Values);
        return new SqlRowSet(cmd, cmd.ExecuteReader());
    }


    public SqlRowSet Read(string command, Dictionary<string, object> plist)
    {
        var cmd = CreateCommand(command, plist);
        return new SqlRowSet(cmd, cmd.ExecuteReader());
    }

    public int Run(TemplateQuery query)
    {
        var cmd = CreateCommand(query.Text, query.Values);
        return cmd.ExecuteNonQuery();
    }

    public int Run(string command, Dictionary<string, object>? plist = null)
    {
        using (var cmd = CreateCommand(command, plist))
        {
            try
            {
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"RunAsync failed for {command}", ex);
            }
        }
    }

    protected virtual void EnsureCreated(DbColumnInfo property, bool forceDefault)
    {
        if (property.Property.DeclaringEntityType != property.Table.EntityType)
            return;

        var existing = columns[property];
        if (existing != null && existing.IsSame(property))
        {
            return;
        }

        if (forceDefault)
        {
            if (!property.IsNullable)
            {
                if (property.DefaultValue == null)
                {
                    if (existing != null)
                    {
                        property.Table.columnsRenamed.Add((existing, property));
                    }
                    else
                    {
                        property.Table.columnsAdded.Add(property);
                    }
                    throw new InvalidOperationException($"You must specify the default value for property {property} as table contains rows");
                }
            }
        }

        if (existing != null)
        {
            property.Table.columnsRenamed.Add((existing, property));
            string postFix = $"_{DateTime.UtcNow.Ticks}";
            // rename...
            //if (preventRename)
            //{
            //    throw new InvalidOperationException($"Renaming of existing column not allowed from {existing.ColumnName} {existing} to {property.ColumnName}{property}");
            //}
            //RenameColumn(property, postFix);
            existing.Table = property.Table;
            if (this.onColumnChange != null)
            {
                existing.ColumnName = property.ColumnName;
                existing.TableNameAndColumnName = property.TableNameAndColumnName;
                existing.EscapedTableNameAndColumnName = property.EscapedTableNameAndColumnName;
                existing.EscapedColumnName = property.EscapedColumnName;
                this.onColumnChange(this, existing, property, postFix);
                handler.OnColumnChanged(property, existing);
                return;
            }
            else
            {
                RenameColumn(property, postFix);
            }
        }
        else
        {
            property.Table.columnsAdded.Add(property);
        }

        AddColumn(property);

        handler.OnColumnAdded(property, existing);

    }

    public abstract string LoadIndexes(IEntityType entity);

    protected void MigrateEntity(DbTableInfo table)
    {

        this.columns.Clear(table);

        if (!this.columns.Exists(table))
        {
            var keys = table.EntityType.GetProperties().Where(x => x.IsKey())
                .Select(x => new DbColumnInfo(table, x, Escape))
                .ToList();
            CreateTable(table, keys);
            handler.OnTableCreated(table);
        }

        var forceDefault = HasAnyRows(table);

        foreach (var property in table.EntityType.GetProperties().Where(x => !x.IsKey()))
        {
            var column = new DbColumnInfo(table, property, Escape);
            EnsureCreated(column, forceDefault);
        }
    }

    protected void PostMigrateEntity(DbTableInfo table)
    {
        // create indexes...
        foreach (var index in table.EntityType.GetIndexes())
        {
            // if all properties are part of primary key
            // and it is unique
            // we should ignore it...
            if (index.IsUnique)
            {
                if (index.Properties.All(x => x.IsKey()))
                    continue;
            }
            var i = new SqlIndexEx(table, index, this);
            EnsureCreated(i);
        }

        handler.OnTableModified(table, table.columnsAdded, table.columnsRenamed, table.indexedUpdated);
    }

    protected abstract bool HasAnyRows(DbTableInfo table);

    protected abstract void CreateTable(DbTableInfo entity, List<DbColumnInfo> keys);

    protected void EnsureCreated(SqlIndexEx index)
    {
        var existing = columns[index];
        if (existing != null && existing.IsSame(index, this))
        {
            return;
        }

        if (existing != null)
        {
            // rename...
            DropIndex(index);
            handler.OnIndexDropped(index);
        }

        CreateIndex(index);
        handler.OnIndexCreated(index);
        index.Table.indexedUpdated.Add((existing != null, index));
    }

    protected abstract void DropIndex(SqlIndexEx index);
    protected abstract void CreateIndex(SqlIndexEx index);
    protected abstract void AddColumn(DbColumnInfo property);

    protected abstract void RenameColumn(DbColumnInfo property, string postFix);


    protected abstract string ToColumn(DbColumnInfo column);
}