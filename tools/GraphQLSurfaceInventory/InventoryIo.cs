using System.Text;
using System.Text.Json;

namespace GraphQLSurfaceInventory;

internal static class InventoryIo
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static InventorySnapshot LoadBaseline(string baselinePath)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            return new InventorySnapshot(DateTime.UtcNow.ToString("O"), []);
        }

        var text = File.ReadAllText(baselinePath);
        return JsonSerializer.Deserialize<InventorySnapshot>(text, JsonOptions)
            ?? new InventorySnapshot(DateTime.UtcNow.ToString("O"), []);
    }

    public static void WriteSnapshot(InventorySnapshot snapshot, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine);
    }

    public static void WriteMarkdownReport(
        InventorySnapshot snapshot,
        IReadOnlyList<MissingCoverage> missingCoverage,
        string reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

        var sensitive = snapshot.Operations
            .Where(op => op.IsSensitive)
            .OrderBy(op => op.Domain, StringComparer.Ordinal)
            .ThenBy(op => op.Kind, StringComparer.Ordinal)
            .ThenBy(op => op.GraphQlName, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# GraphQL Surface Inventory Report");
        sb.AppendLine();
        sb.AppendLine($"> Generated at `{snapshot.GeneratedAtUtc}`");
        sb.AppendLine();
        sb.AppendLine($"- Total operations: **{snapshot.Operations.Count}**");
        sb.AppendLine($"- Sensitive operations: **{sensitive.Count}**");
        sb.AppendLine($"- Newly added sensitive operations missing required coverage: **{missingCoverage.Count}**");
        sb.AppendLine();

        sb.AppendLine("## Gate status");
        sb.AppendLine();
        if (missingCoverage.Count == 0)
        {
            sb.AppendLine("✅ No newly added sensitive operations are missing auth/ownership coverage.");
        }
        else
        {
            sb.AppendLine("❌ Missing coverage detected for newly added sensitive operations:");
            sb.AppendLine();
            foreach (var item in missingCoverage)
            {
                sb.AppendLine($"- `{item.Operation.Kind} {item.Operation.GraphQlName}` ({item.Operation.Domain}) — {item.Reason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Sensitive operation inventory");
        sb.AppendLine();
        sb.AppendLine("| Domain | Kind | GraphQL operation | Explicit [Authorize] | Negative coverage | Positive coverage | Source |\n|---|---|---|---|---|---|---|");

        foreach (var op in sensitive)
        {
            sb.AppendLine($"| {op.Domain} | {op.Kind} | `{op.GraphQlName}` | {(op.HasExplicitAuthorize ? "Yes" : "No")} | {(op.Coverage.HasNegativeCoverage ? "Yes" : "No")} | {(op.Coverage.HasPositiveCoverage ? "Yes" : "No")} | `{Path.GetFileName(op.SourceFile)}` |");
        }

        File.WriteAllText(reportPath, sb.ToString());
    }
}
