using GraphQLSurfaceInventory;

try
{
    var options = CliOptions.Parse(args);

    if (string.IsNullOrWhiteSpace(options.ApiTypesDir)
        || string.IsNullOrWhiteSpace(options.ApiTestsDir)
        || string.IsNullOrWhiteSpace(options.BaselinePath)
        || string.IsNullOrWhiteSpace(options.OutputPath)
        || string.IsNullOrWhiteSpace(options.ReportPath))
    {
        Console.Error.WriteLine("Usage: dotnet run -- --api-types-dir <dir> --api-tests-dir <dir> --baseline <path> --output <path> --report <path> [--gate]");
        return 2;
    }

    var current = SurfaceInventoryAnalyzer.Analyze(options.ApiTypesDir, options.ApiTestsDir);
    var baseline = InventoryIo.LoadBaseline(options.BaselinePath);
    var missing = SurfaceInventoryAnalyzer.FindMissingCoverageForNewSensitiveOperations(current, baseline);

    InventoryIo.WriteSnapshot(current, options.OutputPath);
    InventoryIo.WriteMarkdownReport(current, missing, options.ReportPath);

    Console.WriteLine($"GraphQL surface inventory generated: {current.Operations.Count} operations ({current.Operations.Count(op => op.IsSensitive)} sensitive).");
    Console.WriteLine($"Report: {options.ReportPath}");
    Console.WriteLine($"Snapshot: {options.OutputPath}");

    if (missing.Count == 0)
    {
        Console.WriteLine("No newly added sensitive operations are missing auth/ownership coverage.");
        return 0;
    }

    Console.Error.WriteLine("Missing auth/ownership coverage for newly added sensitive operations:");
    foreach (var item in missing)
    {
        Console.Error.WriteLine($" - {item.Operation.Kind} {item.Operation.GraphQlName} ({item.Operation.Domain}): {item.Reason}");
    }

    return options.Gate ? 1 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"GraphQL surface inventory failed: {ex.Message}");
    return 1;
}
