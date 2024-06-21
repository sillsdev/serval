namespace SIL.DataAccess;

public class ObjectIdGenerator : IIdGenerator
{
    public string GenerateId()
    {
        return ObjectId.GenerateNewId().ToString();
    }
}
