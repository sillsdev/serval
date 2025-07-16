using ExpressionVisitor = System.Linq.Expressions.ExpressionVisitor;

namespace SIL.DataAccess;

public class MongoLinqMethodRewriter : ExpressionVisitor
{
    private static readonly MethodInfo MarkerFirstMatchingElement = typeof(DataAccessExtensions)
        .GetMethods()
        .Single(m => m.Name == nameof(DataAccessExtensions.FirstMatchingElement));

    private static readonly MethodInfo MongoFirstMatchingElement = typeof(MongoEnumerable)
        .GetMethods()
        .Single(m => m.Name == nameof(MongoEnumerable.FirstMatchingElement) && m.GetParameters().Length == 1);

    private static readonly MethodInfo MarkerAllElements = typeof(DataAccessExtensions)
        .GetMethods()
        .Single(m => m.Name == nameof(DataAccessExtensions.AllElements));

    private static readonly MethodInfo MongoAllElements = typeof(MongoEnumerable)
        .GetMethods()
        .Single(m => m.Name == nameof(MongoEnumerable.AllElements) && m.GetParameters().Length == 1);

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.IsGenericMethod)
        {
            Type genericArg = node.Method.GetGenericArguments().First();
            if (node.Method.GetGenericMethodDefinition() == MarkerFirstMatchingElement)
            {
                return Expression.Call(
                    MongoFirstMatchingElement.MakeGenericMethod(genericArg),
                    Visit(node.Arguments.First())
                );
            }

            if (node.Method.GetGenericMethodDefinition() == MarkerAllElements)
                return Expression.Call(MongoAllElements.MakeGenericMethod(genericArg), Visit(node.Arguments.First()));
        }

        return base.VisitMethodCall(node);
    }
}
