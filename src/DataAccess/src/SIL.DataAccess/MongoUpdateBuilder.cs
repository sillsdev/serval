namespace SIL.DataAccess;

public class MongoUpdateBuilder<T> : IUpdateBuilder<T>
    where T : IEntity
{
    private readonly UpdateDefinitionBuilder<T> _builder;
    private readonly List<UpdateDefinition<T>> _defs;
    private readonly Dictionary<BsonDocument, (string Id, ArrayFilterDefinition<BsonValue> FilterDef)> _arrayFilters;

    public MongoUpdateBuilder()
    {
        _builder = Builders<T>.Update;
        _defs = new List<UpdateDefinition<T>>();
        _arrayFilters = new Dictionary<BsonDocument, (string, ArrayFilterDefinition<BsonValue>)>();
    }

    public IUpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value)
    {
        _defs.Add(_builder.Set(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> SetOnInsert<TField>(Expression<Func<T, TField>> field, TField value)
    {
        _defs.Add(_builder.SetOnInsert(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> Unset<TField>(Expression<Func<T, TField>> field)
    {
        _defs.Add(_builder.Unset(ToFieldDefinition(field)));
        return this;
    }

    public IUpdateBuilder<T> Inc(Expression<Func<T, int>> field, int value = 1)
    {
        _defs.Add(_builder.Inc(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> RemoveAll<TItem>(
        Expression<Func<T, IEnumerable<TItem>?>> field,
        Expression<Func<TItem, bool>>? predicate = null
    )
    {
        _defs.Add(_builder.PullFilter(ToFieldDefinition(field), Builders<TItem>.Filter.Where(predicate)));
        return this;
    }

    public IUpdateBuilder<T> Remove<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value)
    {
        _defs.Add(_builder.Pull(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> Add<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value)
    {
        _defs.Add(_builder.Push(ToFieldDefinition(field), value));
        return this;
    }

    public IUpdateBuilder<T> SetAll<TItem, TField>(
        Expression<Func<T, IEnumerable<TItem>?>> collectionField,
        Expression<Func<TItem, TField>> itemField,
        TField value,
        Expression<Func<TItem, bool>>? predicate = null
    )
    {
        Expression<Func<T, TItem>> itemExpr = ExpressionHelper.Concatenate(
            collectionField,
            (collection) => ((IReadOnlyList<TItem>?)collection)![ArrayPosition.ArrayFilter]
        );
        Expression<Func<T, TField>> fieldExpr = ExpressionHelper.Concatenate(itemExpr, itemField);
        if (predicate != null)
        {
            ExpressionFilterDefinition<TItem> filter = new(predicate);
            BsonDocument bsonDoc = filter.Render(
                BsonSerializer.SerializerRegistry.GetSerializer<TItem>(),
                BsonSerializer.SerializerRegistry,
                LinqProvider.V2
            );
            string filterId;
            if (_arrayFilters.TryGetValue(bsonDoc, out var existingArrayFilter))
            {
                filterId = existingArrayFilter.Id;
            }
            else
            {
                filterId = "f" + ObjectId.GenerateNewId().ToString();
                _arrayFilters.Add(
                    bsonDoc,
                    (
                        filterId,
                        new BsonDocument(
                            $"{filterId}.{bsonDoc.Elements.Single().Name}",
                            bsonDoc.Elements.Single().Value
                        )
                    )
                );
            }
            _defs.Add(_builder.Set(ToFieldDefinition(fieldExpr, filterId), value));
        }
        else
        {
            _defs.Add(_builder.Set(ToFieldDefinition(fieldExpr), value));
        }
        return this;
    }

    public (UpdateDefinition<T>, IReadOnlyList<ArrayFilterDefinition>) Build()
    {
        ArrayFilterDefinition[] arrayFilters = _arrayFilters.Values.Select(f => f.FilterDef).ToArray();
        if (_defs.Count == 1)
            return (_defs.Single(), arrayFilters);
        return (_builder.Combine(_defs), arrayFilters);
    }

    private static FieldDefinition<T, TField> ToFieldDefinition<TField>(
        Expression<Func<T, TField>> field,
        string arrayFilterId = ""
    )
    {
        return new DataAccessFieldDefinition<T, TField>(field, arrayFilterId);
    }
}
