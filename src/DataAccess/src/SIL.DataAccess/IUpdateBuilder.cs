namespace SIL.DataAccess;

public interface IUpdateBuilder<T>
    where T : IEntity
{
    IUpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value);

    IUpdateBuilder<T> SetOnInsert<TField>(Expression<Func<T, TField>> field, TField value);

    IUpdateBuilder<T> Unset<TField>(Expression<Func<T, TField>> field);

    IUpdateBuilder<T> Inc(Expression<Func<T, int>> field, int value = 1);

    IUpdateBuilder<T> RemoveAll<TItem>(
        Expression<Func<T, IEnumerable<TItem>?>> field,
        Expression<Func<TItem, bool>>? predicate = null
    );

    IUpdateBuilder<T> Remove<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value);

    IUpdateBuilder<T> Add<TItem>(Expression<Func<T, IEnumerable<TItem>?>> field, TItem value);

    IUpdateBuilder<T> SetAll<TItem, TField>(
        Expression<Func<T, IEnumerable<TItem>?>> collectionField,
        Expression<Func<TItem, TField>> itemField,
        TField value,
        Expression<Func<TItem, bool>>? predicate = null
    );
}
