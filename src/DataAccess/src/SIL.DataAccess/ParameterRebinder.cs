namespace SIL.DataAccess;

internal class ParameterRebinder(ParameterExpression parameter) : System.Linq.Expressions.ExpressionVisitor
{
    private readonly ParameterExpression _parameter = parameter;

    protected override Expression VisitParameter(ParameterExpression p)
    {
        return base.VisitParameter(_parameter);
    }
}
