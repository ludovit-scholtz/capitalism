using System.Text.Json;
using GraphQLSurfaceInventory;

namespace GraphQLSurfaceInventory.Tests;

public sealed class SurfaceInventoryAnalyzerTests
{
    [Fact]
    public void Analyze_ParsesDomainGraphQlNameAndCoverage()
    {
        using var fixture = new TempFixture();

        fixture.WriteTypeFile(
            "Mutation.Lending.cs",
            """
            namespace Api.Types;
            public sealed partial class Mutation
            {
                [Authorize]
                public async Task<Loan> AcceptLoan(AcceptLoanInput input) => new();
            }
            """);

        fixture.WriteTypeFile(
            "Query.Rankings.cs",
            """
            namespace Api.Types;
            public sealed partial class Query
            {
                [Authorize]
                [GraphQLName("companyRankings")]
                public async Task<List<object>> GetCompanyRankings() => [];
            }
            """);

        fixture.WriteTestFile(
            "LoanTests.cs",
            """
            namespace Api.Tests;
            public sealed class LoanTests
            {
                public async Task AcceptLoan_Unauthenticated_ReturnsAuthError() { }
                public async Task AcceptLoan_OwnedCompany_Succeeds() { }
            }
            """);

        fixture.WriteTestFile(
            "RankingTests.cs",
            """
            namespace Api.Tests;
            public sealed class RankingTests
            {
                public async Task GetCompanyRankings_Unauthenticated_ReturnsAuthError() { }
                public async Task GetCompanyRankings_ReturnsOwnedCompanyRankings() { }
            }
            """);

        var snapshot = SurfaceInventoryAnalyzer.Analyze(fixture.TypesDir, fixture.TestsDir);

        var lending = Assert.Single(snapshot.Operations, op => op.MethodName == "AcceptLoan");
        Assert.Equal("lending", lending.Domain);
        Assert.Equal("acceptLoan", lending.GraphQlName);
        Assert.True(lending.HasExplicitAuthorize);
        Assert.True(lending.Coverage.HasNegativeCoverage);
        Assert.True(lending.Coverage.HasPositiveCoverage);

        var ranking = Assert.Single(snapshot.Operations, op => op.MethodName == "GetCompanyRankings");
        Assert.Equal("ranking", ranking.Domain);
        Assert.Equal("companyRankings", ranking.GraphQlName);
    }

    [Fact]
    public void FindMissingCoverageForNewSensitiveOperations_FlagsUncoveredOperation()
    {
        using var fixture = new TempFixture();

        fixture.WriteTypeFile(
            "Mutation.Admin.cs",
            """
            namespace Api.Types;
            public sealed partial class Mutation
            {
                [Authorize]
                public async Task<bool> AdminDeletePlayer() => true;
            }
            """);

        fixture.WriteTestFile(
            "AdminTests.cs",
            """
            namespace Api.Tests;
            public sealed class AdminTests
            {
                public async Task AdminDeletePlayer_Succeeds() { }
            }
            """);

        var current = SurfaceInventoryAnalyzer.Analyze(fixture.TypesDir, fixture.TestsDir);
        var baseline = new InventorySnapshot(DateTime.UtcNow.ToString("O"), []);

        var missing = SurfaceInventoryAnalyzer.FindMissingCoverageForNewSensitiveOperations(current, baseline);

        var item = Assert.Single(missing);
        Assert.Equal("adminDeletePlayer", item.Operation.GraphQlName);
        Assert.Equal("missing unauthenticated/wrong-owner test", item.Reason);
    }

    [Fact]
    public void FindMissingCoverageForNewSensitiveOperations_PassesWhenBothCoverageModesExist()
    {
        using var fixture = new TempFixture();

        fixture.WriteTypeFile(
            "Mutation.StockExchange.cs",
            """
            namespace Api.Types;
            public sealed partial class Mutation
            {
                [Authorize]
                public async Task<object> BuyShares() => new();
            }
            """);

        fixture.WriteTestFile(
            "StockTests.cs",
            """
            namespace Api.Tests;
            public sealed class StockTests
            {
                public async Task BuyShares_ForeignCompany_ReturnsNotOwnedOrNotFound() { }
                public async Task BuyShares_OwnedCompany_Succeeds() { }
            }
            """);

        var current = SurfaceInventoryAnalyzer.Analyze(fixture.TypesDir, fixture.TestsDir);
        var baseline = new InventorySnapshot(DateTime.UtcNow.ToString("O"), []);

        var missing = SurfaceInventoryAnalyzer.FindMissingCoverageForNewSensitiveOperations(current, baseline);

        Assert.Empty(missing);
    }

    [Fact]
    public void WriteMarkdownReport_ContainsExpectedSections()
    {
        using var fixture = new TempFixture();

        var snapshot = new InventorySnapshot(
            DateTime.UtcNow.ToString("O"),
            [
                new OperationInventory(
                    "mutation",
                    "BuyShares",
                    "buyShares",
                    "shareholder",
                    "Mutation.StockExchange.cs",
                    true,
                    new CoverageStatus(true, true, ["BuyShares_ForeignCompany_ReturnsNotOwnedOrNotFound"], ["BuyShares_OwnedCompany_Succeeds"]))
            ]);

        var missing = new List<MissingCoverage>
        {
            new(snapshot.Operations[0], "missing unauthenticated/wrong-owner test"),
        };

        InventoryIo.WriteMarkdownReport(snapshot, missing, fixture.ReportPath);

        var markdown = File.ReadAllText(fixture.ReportPath);
        Assert.Contains("# GraphQL Surface Inventory Report", markdown);
        Assert.Contains("## Gate status", markdown);
        Assert.Contains("`buyShares`", markdown);
    }

    [Fact]
    public void SnapshotRoundtrip_UsesJsonShapeForBaseline()
    {
        using var fixture = new TempFixture();
        var snapshot = new InventorySnapshot(
            DateTime.UtcNow.ToString("O"),
            [
                new OperationInventory(
                    "query",
                    "GetLoanOffers",
                    "loanOffers",
                    "lending",
                    "Query.Lending.cs",
                    true,
                    new CoverageStatus(true, true, ["GetLoanOffers_Unauthenticated_ReturnsAuthError"], ["GetLoanOffers_ReturnsOwnOffers"]))
            ]);

        InventoryIo.WriteSnapshot(snapshot, fixture.BaselinePath);
        var baseline = InventoryIo.LoadBaseline(fixture.BaselinePath);

        Assert.Single(baseline.Operations);
        Assert.Equal("loanOffers", baseline.Operations[0].GraphQlName);

        var document = JsonDocument.Parse(File.ReadAllText(fixture.BaselinePath));
        Assert.True(document.RootElement.TryGetProperty("operations", out _));
    }

    private sealed class TempFixture : IDisposable
    {
        private readonly string _root;

        public TempFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "graphql-surface-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(TypesDir);
            Directory.CreateDirectory(TestsDir);
        }

        public string TypesDir => Path.Combine(_root, "types");

        public string TestsDir => Path.Combine(_root, "tests");

        public string BaselinePath => Path.Combine(_root, "baseline.json");

        public string ReportPath => Path.Combine(_root, "report.md");

        public void WriteTypeFile(string fileName, string content)
            => File.WriteAllText(Path.Combine(TypesDir, fileName), content);

        public void WriteTestFile(string fileName, string content)
            => File.WriteAllText(Path.Combine(TestsDir, fileName), content);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
