using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Core.Recording.FrameRecord;

namespace AbilityKit.Record.Tools;

internal static class Program
{
    private const int ExitMatched = 0;
    private const int ExitDiverged = 1;
    private const int ExitError = 2;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && string.Equals(args[0], "state-diff", StringComparison.OrdinalIgnoreCase))
            {
                return RunStateDiff(args);
            }

            if (!TryParse(args, out var options, out var error))
            {
                Console.Error.WriteLine(error);
                WriteUsage();
                return ExitError;
            }

            var left = FrameRecordCodecs.Current.Load(options.LeftPath);
            var right = FrameRecordCodecs.Current.Load(options.RightPath);
            var report = new FrameRecordDiffAnalyzer().Compare(
                left,
                right,
                new FrameRecordDiffOptions { ContextFrames = options.ContextFrames });
            var json = JsonSerializer.Serialize(report, CreateJsonOptions(options.Indented));

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                var fullOutputPath = Path.GetFullPath(options.OutputPath);
                var directory = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(fullOutputPath, json + Environment.NewLine);
            }

            return report.Matched ? ExitMatched : ExitDiverged;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitError;
        }
    }

    private static int RunStateDiff(string[] args)
    {
        if (!TryParseStateDiff(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            WriteUsage();
            return ExitError;
        }

        var jsonOptions = CreateJsonOptions(indented: false);
        jsonOptions.PropertyNameCaseInsensitive = true;
        var left = JsonSerializer.Deserialize<CanonicalStateFile>(File.ReadAllText(options.LeftPath), jsonOptions);
        var right = JsonSerializer.Deserialize<CanonicalStateFile>(File.ReadAllText(options.RightPath), jsonOptions);
        if (left == null || right == null)
        {
            Console.Error.WriteLine("Canonical state file must contain a JSON object.");
            return ExitError;
        }

        var report = new CanonicalStateDiffAnalyzer().Compare(
            left,
            right,
            new CanonicalStateDiffOptions { MaxDifferences = options.MaxDifferences });
        var json = JsonSerializer.Serialize(report, CreateJsonOptions(options.Indented));
        WriteOutput(json, options.OutputPath);
        return report.Matched ? ExitMatched : ExitDiverged;
    }

    private static void WriteOutput(string json, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Out.WriteLine(json);
            return;
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(fullOutputPath, json + Environment.NewLine);
    }

    private static JsonSerializerOptions CreateJsonOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static bool TryParse(
        string[] args,
        out DiffCommandOptions options,
        out string error)
    {
        options = new DiffCommandOptions();
        error = string.Empty;
        if (args.Length < 3 || !string.Equals(args[0], "diff", StringComparison.OrdinalIgnoreCase))
        {
            error = "Expected the diff command and two record paths.";
            return false;
        }

        options.LeftPath = args[1];
        options.RightPath = args[2];
        for (var i = 3; i < args.Length; i++)
        {
            var argument = args[i];
            if (string.Equals(argument, "--indented", StringComparison.OrdinalIgnoreCase))
            {
                options.Indented = true;
                continue;
            }

            if (string.Equals(argument, "--context", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || !int.TryParse(args[i], out var contextFrames) || contextFrames < 0)
                {
                    error = "--context requires a non-negative integer.";
                    return false;
                }

                options.ContextFrames = contextFrames;
                continue;
            }

            if (string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                {
                    error = "--output requires a file path.";
                    return false;
                }

                options.OutputPath = args[i];
                continue;
            }

            error = $"Unknown argument: {argument}";
            return false;
        }

        if (!File.Exists(options.LeftPath))
        {
            error = $"Left record does not exist: {options.LeftPath}";
            return false;
        }

        if (!File.Exists(options.RightPath))
        {
            error = $"Right record does not exist: {options.RightPath}";
            return false;
        }

        return true;
    }

    private static bool TryParseStateDiff(
        string[] args,
        out StateDiffCommandOptions options,
        out string error)
    {
        options = new StateDiffCommandOptions();
        error = string.Empty;
        if (args.Length < 3)
        {
            error = "Expected the state-diff command and two canonical state paths.";
            return false;
        }

        options.LeftPath = args[1];
        options.RightPath = args[2];
        for (var i = 3; i < args.Length; i++)
        {
            var argument = args[i];
            if (string.Equals(argument, "--indented", StringComparison.OrdinalIgnoreCase))
            {
                options.Indented = true;
                continue;
            }
            if (string.Equals(argument, "--max-differences", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || !int.TryParse(args[i], out var maxDifferences) || maxDifferences <= 0)
                {
                    error = "--max-differences requires a positive integer.";
                    return false;
                }
                options.MaxDifferences = maxDifferences;
                continue;
            }
            if (string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                {
                    error = "--output requires a file path.";
                    return false;
                }
                options.OutputPath = args[i];
                continue;
            }
            error = $"Unknown argument: {argument}";
            return false;
        }

        if (!File.Exists(options.LeftPath))
        {
            error = $"Left canonical state does not exist: {options.LeftPath}";
            return false;
        }
        if (!File.Exists(options.RightPath))
        {
            error = $"Right canonical state does not exist: {options.RightPath}";
            return false;
        }
        return true;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: AbilityKit.Record.Tools diff <left.record.bin|json> <right.record.bin|json> [--context N] [--output report.json] [--indented]");
        Console.Error.WriteLine(
            "       AbilityKit.Record.Tools state-diff <left.state.json> <right.state.json> [--max-differences N] [--output report.json] [--indented]");
    }

    private sealed class DiffCommandOptions
    {
        public string LeftPath { get; set; } = string.Empty;
        public string RightPath { get; set; } = string.Empty;
        public int ContextFrames { get; set; } = 2;
        public string OutputPath { get; set; } = string.Empty;
        public bool Indented { get; set; }
    }

    private sealed class StateDiffCommandOptions
    {
        public string LeftPath { get; set; } = string.Empty;
        public string RightPath { get; set; } = string.Empty;
        public int MaxDifferences { get; set; } = 64;
        public string OutputPath { get; set; } = string.Empty;
        public bool Indented { get; set; }
    }
}
