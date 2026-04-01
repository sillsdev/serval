namespace SIL.DataAccess;

public static class Attempt
{
    public static Attempt<T> Success<T>(T result)
    {
        return new Attempt<T>(true, result);
    }

    public static Attempt<T> Failure<T>(T result)
    {
        return new Attempt<T>(false, result);
    }
}

public readonly struct Attempt<T>(bool success, T? result = default)
{
    public static Attempt<T> Failure { get; }

    public Attempt(T result)
        : this(true, result) { }

    public T? Result { get; } = result;
    public bool Success { get; } = success;

    public bool TryResult([MaybeNullWhen(false)] out T result)
    {
        result = Result;
        return Success;
    }
}
