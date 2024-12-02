namespace SIL.DataAccess;

public class MongoUpdateBuilder<T> : IUpdateBuilder<T>
    where T : IEntity
{
    private readonly UpdateDefinitionBuilder<T> _updateBuilder;
    private readonly FilterDefinitionBuilder<T> _filterBuilder;
    private readonly List<UpdateDefinition<T>> _defs;

    public MongoUpdateBuilder()
    {
        _updateBuilder = Builders<T>.Update;
        _filterBuilder = Builders<T>.Filter;
        _defs = new List<UpdateDefinition<T>>();
    }

    public IUpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value)
    {
        _defs.Add(_updateBuilder.Set(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> SetOnInsert<TField>(Expression<Func<T, TField>> field, TField value)
    {
        _defs.Add(_updateBuilder.SetOnInsert(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> Unset<TField>(Expression<Func<T, TField>> field)
    {
        _defs.Add(_updateBuilder.Unset(ToFieldDefinition(field)));
        return this;
    }

    public IUpdateBuilder<T> Inc(Expression<Func<T, int>> field, int value = 1)
    {
        _defs.Add(_updateBuilder.Inc(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> RemoveAll<TItem>(
        Expression<Func<T, IEnumerable<TItem>?>> field,
        Expression<Func<TItem, bool>> predicate
    )
    {
        _defs.Add(_updateBuilder.PullFilter(ToFieldDefinition(field), Builders<TItem>.Filter.Where(predicate)));
        return this;
    }

    public IUpdateBuilder<T> Remove<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value)
    {
        _defs.Add(_updateBuilder.Pull(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> Add<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value)
    {
        _defs.Add(_updateBuilder.Push(ToFieldDefinition(field), value));
        return this;
    }

    public UpdateDefinition<T> Build()
    {
        if (_defs.Count == 1)
            return _defs.Single();
        return _updateBuilder.Combine(_defs);
    }

    private static FieldDefinition<T, TField> ToFieldDefinition<TField>(Expression<Func<T, TField>> field)
    {
        return new DataAccessFieldDefinition<T, TField>(field);
    }
}
