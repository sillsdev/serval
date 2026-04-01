namespace SIL.DataAccess;

public static class DataAccessExtensions
{
    public static async Task CreateOrUpdateAsync<T>(this IMongoIndexManager<T> indexes, CreateIndexModel<T> indexModel)
    {
        try
        {
            await indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
        }
        catch (MongoCommandException ex)
        {
            if (ex.CodeName == "IndexOptionsConflict")
            {
                string name = ex.Command["indexes"][0]["name"].AsString;
                await indexes.DropOneAsync(name).ConfigureAwait(false);
                await indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            else
            {
                throw;
            }
        }
    }

    public static T FirstMatchingElement<T>(this IEnumerable<T> _) =>
        throw new NotSupportedException(
            $"{nameof(FirstMatchingElement)}() is a marker for {nameof(MongoLinqMethodRewriter)} and should not be called directly."
        );

    public static T AllElements<T>(this IEnumerable<T> _) =>
        throw new NotSupportedException(
            $"{nameof(AllElements)}() is a marker for {nameof(MongoLinqMethodRewriter)} and should not be called directly."
        );
}
