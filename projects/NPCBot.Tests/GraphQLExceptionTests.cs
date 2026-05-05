using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="GraphQLException"/> — a typed exception that carries
/// the machine-readable error code returned by the game API.
/// </summary>
public sealed class GraphQLExceptionTests
{
    [Fact]
    public void Constructor_StoresCode()
    {
        var ex = new GraphQLException("Something went wrong.", "DUPLICATE_EMAIL");
        Assert.Equal("DUPLICATE_EMAIL", ex.Code);
    }

    [Fact]
    public void Constructor_StoresMessage()
    {
        var ex = new GraphQLException("Something went wrong.", "DUPLICATE_EMAIL");
        Assert.Equal("Something went wrong.", ex.Message);
    }

    [Fact]
    public void Constructor_IsException()
    {
        var ex = new GraphQLException("err", "ERR_CODE");
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void Constructor_EmptyCodeIsAllowed()
    {
        var ex = new GraphQLException("Some message.", string.Empty);
        Assert.Equal(string.Empty, ex.Code);
    }

    [Fact]
    public void Constructor_CodeAndMessageAreIndependent()
    {
        var ex = new GraphQLException("The message", "THE_CODE");
        Assert.NotEqual(ex.Message, ex.Code);
    }

    [Fact]
    public void Constructor_CanBeCaughtAsException()
    {
        Exception? caught = null;
        try
        {
            throw new GraphQLException("test", "TEST");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<GraphQLException>(caught);
        Assert.Equal("TEST", ((GraphQLException)caught).Code);
    }

    [Fact]
    public void Constructor_CanMatchByCode()
    {
        var ex = new GraphQLException("Duplicate email.", "DUPLICATE_EMAIL");
        // Pattern used in AccountService to detect duplicate-registration errors.
        Assert.True(ex.Code == "DUPLICATE_EMAIL");
    }
}
