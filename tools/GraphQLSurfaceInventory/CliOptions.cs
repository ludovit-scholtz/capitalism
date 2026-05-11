namespace GraphQLSurfaceInventory;

internal sealed class CliOptions
{
    public string ApiTypesDir { get; init; } = string.Empty;

    public string ApiTestsDir { get; init; } = string.Empty;

    public string BaselinePath { get; init; } = string.Empty;

    public string OutputPath { get; init; } = string.Empty;

    public string ReportPath { get; init; } = string.Empty;

    public bool Gate { get; init; }

    public static CliOptions Parse(string[] args)
    {
        static string? ReadValue(string[] values, ref int index)
        {
            if (index + 1 >= values.Length)
            {
                return null;
            }

            index += 1;
            return values[index];
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var gate = false;

        for (var i = 0; i < args.Length; i += 1)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--gate":
                    gate = true;
                    break;
                case "--api-types-dir":
                case "--api-tests-dir":
                case "--baseline":
                case "--output":
                case "--report":
                    var value = ReadValue(args, ref i);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException($"Missing value for {arg}.");
                    }

                    values[arg] = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return new CliOptions
        {
            ApiTypesDir = values.GetValueOrDefault("--api-types-dir") ?? string.Empty,
            ApiTestsDir = values.GetValueOrDefault("--api-tests-dir") ?? string.Empty,
            BaselinePath = values.GetValueOrDefault("--baseline") ?? string.Empty,
            OutputPath = values.GetValueOrDefault("--output") ?? string.Empty,
            ReportPath = values.GetValueOrDefault("--report") ?? string.Empty,
            Gate = gate,
        };
    }
}
