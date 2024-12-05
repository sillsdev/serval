namespace SIL.DataAccess;

internal class ParameterReplacer(ParameterExpression oldExpression, Expression newExpression)
    : System.Linq.Expressions.ExpressionVisitor
{
    private readonly ParameterExpression _oldExpression = oldExpression;
    private readonly Expression _newExpression = newExpression;

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == _oldExpression)
            return _newExpression;

        return base.VisitParameter(node);
    }
}
