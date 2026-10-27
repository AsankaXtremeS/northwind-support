using System.Linq.Expressions;
using System.Reflection;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Lets a query reuse an expression defined elsewhere, such as a domain rule. Inside a lambda,
/// write <c>rule.Inline(x.Ticket)</c>; pass the lambda through <see cref="Expand{TDelegate}"/>
/// and the call is replaced by the rule's own body, which EF Core can translate to SQL.
/// </summary>
internal static class ExpressionInlining
{
    private static readonly MethodInfo InlineMethod =
        typeof(ExpressionInlining).GetMethod(nameof(Inline))!;

    /// <summary>A placeholder that only means something inside an expanded expression.</summary>
    public static TResult Inline<TSource, TResult>(this Expression<Func<TSource, TResult>> expression, TSource source) =>
        throw new InvalidOperationException($"{nameof(Inline)} is only valid inside an expression passed through {nameof(Expand)}.");

    public static Expression<TDelegate> Expand<TDelegate>(this Expression<TDelegate> expression) =>
        (Expression<TDelegate>)new InlineExpander().Visit(expression);

    private sealed class InlineExpander : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod || node.Method.GetGenericMethodDefinition() != InlineMethod)
            {
                return base.VisitMethodCall(node);
            }

            var inlined = (LambdaExpression)Evaluate(node.Arguments[0]);
            var argument = Visit(node.Arguments[1]);

            return Visit(new ParameterReplacer(inlined.Parameters[0], argument).Visit(inlined.Body));
        }

        /// <summary>Reads the expression being inlined, normally a captured local variable.</summary>
        private static object Evaluate(Expression expression) => expression switch
        {
            ConstantExpression constant => constant.Value!,
            MemberExpression { Expression: ConstantExpression closure, Member: FieldInfo field } =>
                field.GetValue(closure.Value)!,
            _ => Expression.Lambda(expression).Compile().DynamicInvoke()!
        };
    }

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
