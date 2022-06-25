public static class PropertyExtensions
{

    public static string GetSchemaOrDefault(this IEntityType type)
    {
        return type.GetSchema() ?? type.GetDefaultSchema() ?? "dbo";
    }

}