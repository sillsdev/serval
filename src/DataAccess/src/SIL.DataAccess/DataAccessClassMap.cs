namespace SIL.DataAccess;

public static class DataAccessClassMap
{
    public static void RegisterConventions(string nspace, params IConvention[] conventions)
    {
        ConventionRegistry.Remove(nspace);
        var conventionPack = new ConventionPack();
        conventionPack.AddRange(conventions);
        ConventionRegistry.Register(nspace, conventionPack, t => t.Namespace != null && t.Namespace.StartsWith(nspace));
    }

    public static void RegisterClass<T>(Action<BsonClassMap<T>> mapSetup)
    {
        BsonClassMap.TryRegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            mapSetup?.Invoke(cm);
        });
    }
}
