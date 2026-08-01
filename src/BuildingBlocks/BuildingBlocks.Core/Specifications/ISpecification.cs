using System.Linq.Expressions;

namespace BuildingBlocks.Core.Specifications;

/// <summary>
/// 规格模式接口 — 将查询条件封装为独立对象，可组合复用。
/// </summary>
public interface ISpecification<T> where T : class
{
    /// <summary>查询条件表达式</summary>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>是否满足规格</summary>
    bool IsSatisfiedBy(T entity);

    /// <summary>And 组合</summary>
    ISpecification<T> And(ISpecification<T> other);

    /// <summary>Or 组合</summary>
    ISpecification<T> Or(ISpecification<T> other);

    /// <summary>Not 取反</summary>
    ISpecification<T> Not();
}

/// <summary>
/// 规格模式基类 — 提供 And / Or / Not 组合逻辑（Composite 模式）。
/// </summary>
public abstract class Specification<T>(Expression<Func<T, bool>> expression) : ISpecification<T>
    where T : class
{
    private readonly Expression<Func<T, bool>> _expression = expression;

    public Expression<Func<T, bool>> ToExpression() => _expression;

    public bool IsSatisfiedBy(T entity) => _expression.Compile()(entity);

    public ISpecification<T> And(ISpecification<T> other)
        => new AndSpecification<T>(this, other);

    public ISpecification<T> Or(ISpecification<T> other)
        => new OrSpecification<T>(this, other);

    public ISpecification<T> Not()
        => new NotSpecification<T>(this);

    /// <summary>从 Lambda 表达式创建规格</summary>
    public static Specification<T> Create(Expression<Func<T, bool>> expression)
        => new ExpressionSpecification<T>(expression);
}

/// <summary>直接从表达式创建的规格</summary>
internal sealed class ExpressionSpecification<T>(Expression<Func<T, bool>> expression)
    : Specification<T>(expression) where T : class;

/// <summary>And 组合规格</summary>
internal sealed class AndSpecification<T>(ISpecification<T> left, ISpecification<T> right)
    : Specification<T>(CombineAnd(left.ToExpression(), right.ToExpression())) where T : class
{
    private static Expression<Func<T, bool>> CombineAnd(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var visitor = new ParameterReplacer(right.Parameters[0], parameter);
        var rightBody = visitor.Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, rightBody!), parameter);
    }
}

/// <summary>Or 组合规格</summary>
internal sealed class OrSpecification<T>(ISpecification<T> left, ISpecification<T> right)
    : Specification<T>(CombineOr(left.ToExpression(), right.ToExpression())) where T : class
{
    private static Expression<Func<T, bool>> CombineOr(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var visitor = new ParameterReplacer(right.Parameters[0], parameter);
        var rightBody = visitor.Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left.Body, rightBody!), parameter);
    }
}

/// <summary>Not 取反规格</summary>
internal sealed class NotSpecification<T>(ISpecification<T> spec)
    : Specification<T>(CombineNot(spec.ToExpression())) where T : class
{
    private static Expression<Func<T, bool>> CombineNot(Expression<Func<T, bool>> expression)
    {
        return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);
    }
}

/// <summary>参数替换器 — 用于组合表达式时统一参数</summary>
internal sealed class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam)
    : ExpressionVisitor
{
    private readonly ParameterExpression _oldParam = oldParam;
    private readonly ParameterExpression _newParam = newParam;

    protected override Expression VisitParameter(ParameterExpression node)
        => node == _oldParam ? _newParam : base.VisitParameter(node);
}
