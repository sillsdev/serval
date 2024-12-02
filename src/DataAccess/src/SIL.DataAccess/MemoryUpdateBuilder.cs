namespace SIL.DataAccess;

public class MemoryUpdateBuilder<T>(Expression<Func<T, bool>> filter, T entity, bool isInsert) : IUpdateBuilder<T>
    where T : IEntity
{
    private readonly Expression<Func<T, bool>> _filter = filter;
    private readonly T _entity = entity;
    private readonly bool _isInsert = isInsert;

    public IUpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value)
    {
        (IEnumerable<object> owners, PropertyInfo? prop, object? index) = GetFieldOwners(field);
        object[]? indices = index == null ? null : [index];
        foreach (object owner in owners)
            prop.SetValue(owner, value, indices);
        return this;
    }

    public IUpdateBuilder<T> SetOnInsert<TField>(Expression<Func<T, TField>> field, TField value)
    {
        if (_isInsert)
            Set(field, value);
        return this;
    }

    public IUpdateBuilder<T> Unset<TField>(Expression<Func<T, TField>> field)
    {
        (IEnumerable<object> owners, PropertyInfo prop, object? index) = GetFieldOwners(field);
        if (index != null)
        {
            // remove value from a dictionary
            Type dictionaryType = prop.DeclaringType!;
            Type keyType = dictionaryType.GetGenericArguments()[0];
            MethodInfo removeMethod = dictionaryType.GetMethod("Remove", [keyType])!;
            foreach (object owner in owners)
                removeMethod.Invoke(owner, new[] { index });
        }
        else
        {
            // set property to default value
            object? value = null;
            if (prop.PropertyType.IsValueType)
                value = Activator.CreateInstance(prop.PropertyType);
            foreach (object owner in owners)
                prop.SetValue(owner, value);
        }
        return this;
    }

    public IUpdateBuilder<T> Inc(Expression<Func<T, int>> field, int value = 1)
    {
        (IEnumerable<object> owners, PropertyInfo prop, object? index) = GetFieldOwners(field);
        object[]? indices = index == null ? null : [index];
        foreach (object owner in owners)
        {
            int curValue = (int)prop.GetValue(owner, indices)!;
            curValue += value;
            prop.SetValue(owner, curValue, indices);
        }
        return this;
    }

    public IUpdateBuilder<T> RemoveAll<TItem>(
        Expression<Func<T, IEnumerable<TItem>?>> field,
        Expression<Func<TItem, bool>> predicate
    )
    {
        (IEnumerable<object> owners, PropertyInfo? prop, object? index) = GetFieldOwners(field);
        object[]? indices = index == null ? null : [index];
        Func<TItem, bool> predicateFunc = predicate.Compile();
        foreach (object owner in owners)
        {
            var collection = (IEnumerable<TItem>?)prop.GetValue(owner, indices);
            MethodInfo? removeMethod = collection?.GetType().GetMethod("Remove");
            if (collection is not null && removeMethod is not null)
            {
                // the collection is mutable, so use Remove method to remove item
                TItem[] toRemove = collection.Where(predicateFunc).ToArray();
                foreach (TItem item in toRemove)
                    removeMethod.Invoke(collection, [item]);
            }
            else if (collection is not null)
            {
                if (prop.PropertyType.IsArray || prop.PropertyType.IsInterface)
                {
                    // the collection type is an array or interface, so construct a new array and set property
                    TItem[] newValue = collection.Where(i => !predicateFunc(i)).ToArray();
                    prop.SetValue(owner, newValue, indices);
                }
                else
                {
                    // the collection type is a collection class, so construct a new collection and set property
                    var newValue = (IEnumerable<TItem>?)
                        Activator.CreateInstance(prop.PropertyType, collection.Where(i => !predicateFunc(i)).ToArray());
                    prop.SetValue(owner, newValue, indices);
                }
            }
        }
        return this;
    }

    public IUpdateBuilder<T> Remove<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value)
    {
        (IEnumerable<object> owners, PropertyInfo? prop, object? index) = GetFieldOwners(field);
        object[]? indices = index == null ? null : [index];
        foreach (object owner in owners)
        {
            var collection = (IEnumerable<TItem>?)prop.GetValue(owner, indices);
            MethodInfo? removeMethod = collection?.GetType().GetMethod("Remove");
            if (removeMethod is not null)
            {
                // the collection is mutable, so use Remove method to remove item
                removeMethod.Invoke(collection, [value]);
            }
            else if (collection is not null)
            {
                if (prop.PropertyType.IsArray || prop.PropertyType.IsInterface)
                {
                    // the collection type is an array or interface, so construct a new array and set property
                    TItem[] newValue = collection.Except([value]).ToArray();
                    prop.SetValue(owner, newValue, indices);
                }
                else
                {
                    // the collection type is a collection class, so construct a new collection and set property
                    var newValue = (IEnumerable<TItem>?)
                        Activator.CreateInstance(prop.PropertyType, collection.Except([value]).ToArray());
                    prop.SetValue(owner, newValue, indices);
                }
            }
        }
        return this;
    }

    public IUpdateBuilder<T> Add<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value)
    {
        (IEnumerable<object> owners, PropertyInfo? prop, object? index) = GetFieldOwners(field);
        object[]? indices = index == null ? null : [index];
        foreach (object owner in owners)
        {
            var collection = (IEnumerable<TItem>?)prop.GetValue(owner, indices);
            MethodInfo? addMethod = collection?.GetType().GetMethod("Add");
            if (addMethod is not null)
            {
                // the collection is mutable, so use Add method to insert item
                addMethod.Invoke(collection, [value]);
            }
            else
            {
                collection ??= Array.Empty<TItem>();
                if (prop.PropertyType.IsArray || prop.PropertyType.IsInterface)
                {
                    // the collection type is an array or interface, so construct a new array and set property
                    TItem[] newValue = collection.Concat([value]).ToArray();
                    prop.SetValue(owner, newValue, indices);
                }
                else
                {
                    // the collection type is a collection class, so construct a new collection and set property
                    var newValue = (IEnumerable<TItem>?)
                        Activator.CreateInstance(prop.PropertyType, collection.Concat([value]).ToArray());
                    prop.SetValue(owner, newValue, indices);
                }
            }
        }
        return this;
    }

    private static bool IsAnyMethod(MethodInfo mi)
    {
        return mi.DeclaringType == typeof(Enumerable) && mi.Name == "Any";
    }

    private static MethodInfo GetFirstOrDefaultMethod(Type type)
    {
        return typeof(Enumerable)
            .GetMethods()
            .Where(m => m.Name == "FirstOrDefault")
            .Single(m => m.GetParameters().Length == 2 && m.GetParameters()[1].Name == "predicate")
            .MakeGenericMethod(type);
    }

    private (IEnumerable<object> Owners, PropertyInfo Property, object? Index) GetFieldOwners<TField>(
        Expression<Func<T, TField>> field
    )
    {
        List<object>? owners = null;
        MemberInfo? member = null;
        object? index = null;
        foreach (Expression node in ExpressionHelper.Flatten(field))
        {
            var newOwners = new List<object>();
            if (owners == null)
            {
                if (_entity != null)
                    newOwners.Add(_entity);
            }
            else
            {
                foreach (object owner in owners)
                {
                    object? newOwner;
                    switch (member)
                    {
                        case MethodInfo method:
                            switch (index)
                            {
                                case ArrayPosition.FirstMatching:
                                    foreach (Expression expression in ExpressionHelper.Flatten(_filter))
                                    {
                                        if (expression is MethodCallExpression callExpr && IsAnyMethod(callExpr.Method))
                                        {
                                            var predicate = (LambdaExpression)callExpr.Arguments[1];
                                            Type itemType = predicate.Parameters[0].Type;
                                            MethodInfo firstOrDefault = GetFirstOrDefaultMethod(itemType);
                                            newOwner = firstOrDefault.Invoke(
                                                null,
                                                new object[] { owner, predicate.Compile() }
                                            );
                                            if (newOwner != null)
                                                newOwners.Add(newOwner);
                                            break;
                                        }
                                    }
                                    break;
                                case ArrayPosition.All:
                                // This doesn't filter as it should - but it's good enough for unit testing.
                                case ArrayPosition.ArrayFilter:
                                    newOwners.AddRange(((IEnumerable)owner).Cast<object>());
                                    break;
                                default:
                                    if (index == null)
                                        break;
                                    if (owner is IDictionary dict)
                                    {
                                        newOwner = dict[index];
                                        if (newOwner != null)
                                        {
                                            newOwners.Add(newOwner);
                                        }
                                        else
                                        {
                                            Type valueType = owner.GetType().GenericTypeArguments[1];
                                            newOwner = Activator.CreateInstance(valueType);
                                            dict[index] = newOwner;
                                        }
                                    }
                                    else
                                    {
                                        newOwner = method.Invoke(owner, new object[] { index });
                                        if (newOwner != null)
                                            newOwners.Add(newOwner);
                                    }
                                    break;
                            }
                            break;

                        case PropertyInfo prop:
                            newOwner = prop.GetValue(owner);
                            if (newOwner != null)
                                newOwners.Add(newOwner);
                            break;
                    }
                }
            }
            owners = newOwners;

            switch (node)
            {
                case MemberExpression memberExpr:
                    member = memberExpr.Member;
                    index = null;
                    break;

                case MethodCallExpression methodExpr:
                    member = methodExpr.Method;
                    if (member.Name != "get_Item")
                        throw new ArgumentException("Invalid method call in field expression.", nameof(field));

                    Expression argExpr = methodExpr.Arguments[0];
                    index = ExpressionHelper.FindConstantValue(argExpr);
                    break;
            }
        }

        var property = member as PropertyInfo;
        if (property == null && member != null && index != null)
            property = member.DeclaringType!.GetProperty("Item");
        return (owners!, property!, index);
    }
}
