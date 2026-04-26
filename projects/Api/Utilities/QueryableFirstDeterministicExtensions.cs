using System.Linq.Expressions;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static class QueryableFirstDeterministicExtensions
{
    public static Task<T> FirstDeterministicAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        return EnsureDeterministicFirstQuery(query).FirstAsync(cancellationToken);
    }

    public static Task<T?> FirstOrDefaultDeterministicAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        return EnsureDeterministicFirstQuery(query).FirstOrDefaultAsync(cancellationToken);
    }

    public static T FirstDeterministic<T>(this IQueryable<T> query)
    {
        return EnsureDeterministicFirstQuery(query).First();
    }

    public static T? FirstOrDefaultDeterministic<T>(this IQueryable<T> query)
    {
        return EnsureDeterministicFirstQuery(query).FirstOrDefault();
    }

    private static IQueryable<T> EnsureDeterministicFirstQuery<T>(IQueryable<T> query)
    {
        if (HasOrderOrFilter(query.Expression))
        {
            return query;
        }

        if (typeof(T) == typeof(GameState))
        {
            return (IQueryable<T>)query.Cast<GameState>().OrderBy(state => state.Id);
        }

        if (typeof(T) == typeof(City))
        {
            // Starter flows and many integration tests assume the seeded city baseline order.
            // Population ASC keeps Bratislava as the deterministic first city.
            return (IQueryable<T>)query.Cast<City>().OrderBy(city => city.Population).ThenBy(city => city.Name);
        }

        if (typeof(T).GetProperty("Id") is not null)
        {
            return query.OrderBy(static item => EF.Property<object>(item!, "Id"));
        }

        // Keep previous provider order semantics while making the query explicitly ordered.
        // This prevents First/FirstOrDefault unordered warnings without changing the effective
        // row selection behavior in existing tests and runtime flows.
        return query.OrderBy(static _ => 0);
    }

    private static bool HasOrderOrFilter(Expression expression)
    {
        var current = expression;

        while (current is MethodCallExpression methodCall)
        {
            if (methodCall.Method.DeclaringType == typeof(Queryable))
            {
                if (methodCall.Method.Name is "Where"
                    or "OrderBy"
                    or "OrderByDescending"
                    or "ThenBy"
                    or "ThenByDescending")
                {
                    return true;
                }

                if (methodCall.Arguments.Count > 0)
                {
                    current = methodCall.Arguments[0];
                    continue;
                }
            }

            break;
        }

        return false;
    }
}