namespace SIL.DataAccess;

public class ObjectRefConvention : ConventionBase, IMemberMapConvention
{
    public void Apply(BsonMemberMap memberMap)
    {
        if (memberMap.MemberName.EndsWith("Ref") && memberMap.MemberName.Length > 3)
            memberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
    }
}
