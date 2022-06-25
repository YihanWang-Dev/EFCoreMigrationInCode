

public static class MigrationHelperExtensions
{
    public static ModelMigrationBase MigrationForPostGreSql(this DbContext context)
    {
        return new PostGreSqlMigration(context);
    }
}