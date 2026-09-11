using System;
using AbilityKit.Samples.Infrastructure;
using AbilityKit.Samples.Logic.Infrastructure.Config;
using System.Text;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Infrastructure;
using AbilityKit.Samples.Infrastructure.WebStatic;
using AbilityKit.Samples.Logic;

namespace AbilityKit.Samples
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // 示例内容只依赖 IResourceProvider；文件系统实现由 .NET 宿主安装。
            ResourceProviders.Current = new FileSystemResourceProvider();

            var cli = SampleCliOptions.Parse(args);
            if (!string.IsNullOrWhiteSpace(cli.WebOutputDirectory))
            {
                var path = SampleWebExporter.Export(cli.WebOutputDirectory, cli.ToRunOptions());
                Console.WriteLine($"Static sample page generated: {path}");
                Console.WriteLine("Open the file in a browser and refresh it after re-exporting.");
                return 0;
            }

            if (cli.ValidateManifest)
            {
                var report = SampleManifestValidator.Validate();
                foreach (var line in report.ToLines())
                {
                    Console.WriteLine(line);
                }

                return report.HasErrors ? 1 : 0;
            }

            var runner = new SampleRunner(cli.ToRunOptions());
            RegisterSamples(runner);

            if (cli.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            if (cli.ListOnly)
            {
                runner.PrintHeader();
                runner.PrintMenu();
                return 0;
            }

            var request = cli.ToRunRequest();
            if (request != null)
            {
                var passed = runner.Run(request);
                var expected = request.SelectionKind == SampleRunSelectionKind.All ? runner.AllSamples.Count : 1;
                return passed == expected ? 0 : 1;
            }

            RunInteractive(runner);
            return 0;
        }

        private static void RunInteractive(SampleRunner runner)
        {
            runner.PrintHeader();

            while (true)
            {
                runner.PrintMenu();
                Console.Write("Select sample index (Q to quit): ");

                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("Quit", StringComparison.OrdinalIgnoreCase))
                    break;

                if (input.Equals("?", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("Help", StringComparison.OrdinalIgnoreCase))
                {
                    PrintUsage();
                    continue;
                }

                if (int.TryParse(input, out var index))
                    runner.Run(index);
                else
                    Console.WriteLine("Invalid input. Enter a number, ? for help, or Q to quit.");
            }
        }

        private static void RegisterSamples(SampleRunner runner)
        {
            foreach (var entry in SampleCatalogProvider.CreateCatalog().Entries)
            {
                try
                {
                    runner.Register(entry);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to register sample {entry.Id}: {ex.Message}");
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("AbilityKit.Samples usage:");
            Console.WriteLine("  dotnet run --project src/AbilityKit.Samples -- --list");
            Console.WriteLine("  dotnet run --project src/AbilityKit.Samples -- --id onboarding/orientation");
            Console.WriteLine("  dotnet run --project src/AbilityKit.Samples -- --web sample-web");
            Console.WriteLine("  dotnet run --project src/AbilityKit.Samples -- --all --file --output sample-output");
            Console.WriteLine();
            Console.WriteLine("Recommended path:");
            Console.WriteLine("  The default menu lists manifest Stable/Candidate entries. Indexes are temporary; stable ids are configured in sample-manifest.json.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --help                 Show usage.");
            Console.WriteLine("  --list                 Print official manifest sample menu and exit.");
            Console.WriteLine("  --run <index>          Run one sample by index.");
            Console.WriteLine("  --id <stable-id>       Run one sample by manifest id.");
            Console.WriteLine("  --all                  Run all samples.");
            Console.WriteLine("  --mode <name>          instant, simulated, or realtime.");
            Console.WriteLine("  --file                 Write each sample output to a log file.");
            Console.WriteLine("  --output <directory>   Output directory for file logs.");
            Console.WriteLine("  --web [directory]      Export a static web page. No HTTP server required.");
            Console.WriteLine("  --no-console           Disable console sample output.");
            Console.WriteLine("  --validate-manifest    Validate manifest, formal catalog, and output contract quality gates.");
        }

        private sealed class SampleCliOptions
        {
            public bool ShowHelp { get; private set; }
            public bool ListOnly { get; private set; }
            public bool RunAll { get; private set; }
            public int? RunIndex { get; private set; }
            public string RunId { get; private set; } = string.Empty;
            public ExecutionMode Mode { get; private set; } = ExecutionMode.Instant;
            public bool WriteConsole { get; private set; } = true;
            public bool WriteFile { get; private set; }
            public string OutputDirectory { get; private set; } = "sample-output";
            public string WebOutputDirectory { get; private set; } = string.Empty;
            public bool ValidateManifest { get; private set; }

            public SampleRunRequest? ToRunRequest()
            {
                var options = ToRunOptions();
                if (RunAll)
                    return SampleRunRequest.All(options);

                if (RunIndex.HasValue)
                    return SampleRunRequest.ByIndex(RunIndex.Value, options);

                if (!string.IsNullOrWhiteSpace(RunId))
                    return SampleRunRequest.ById(RunId, options);

                return null;
            }

            public SampleRunOptions ToRunOptions()
            {
                return new SampleRunOptions
                {
                    ExecutionMode = Mode,
                    HostKind = SampleHostKind.Console,
                    HostCapabilities = SampleHostCapabilities.ForHost(SampleHostKind.Console),
                    WriteConsole = WriteConsole,
                    WriteFile = WriteFile,
                    OutputDirectory = OutputDirectory
                };
            }

            public static SampleCliOptions Parse(string[] args)
            {
                var options = new SampleCliOptions();

                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ShowHelp = true;
                    }
                    else if (arg.Equals("--list", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ListOnly = true;
                    }
                    else if (arg.Equals("--all", StringComparison.OrdinalIgnoreCase))
                    {
                        options.RunAll = true;
                    }
                    else if (arg.Equals("--file", StringComparison.OrdinalIgnoreCase))
                    {
                        options.WriteFile = true;
                    }
                    else if (arg.Equals("--no-console", StringComparison.OrdinalIgnoreCase))
                    {
                        options.WriteConsole = false;
                    }
                    else if (arg.Equals("--validate-manifest", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ValidateManifest = true;
                    }
                    else if (ReadValue(args, ref i, "--run", out var runValue) && int.TryParse(runValue, out var index))
                    {
                        options.RunIndex = index;
                    }
                    else if (ReadValue(args, ref i, "--id", out var idValue))
                    {
                        options.RunId = idValue;
                    }
                    else if (ReadValue(args, ref i, "--mode", out var modeValue) && TryParseMode(modeValue, out var mode))
                    {
                        options.Mode = mode;
                    }
                    else if (ReadValue(args, ref i, "--output", out var outputDirectory))
                    {
                        options.OutputDirectory = outputDirectory;
                        options.WriteFile = true;
                    }
                    else if (arg.Equals("--web", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                            options.WebOutputDirectory = args[++i];
                        else
                            options.WebOutputDirectory = "sample-web";
                    }
                    else if (arg.StartsWith("--web=", StringComparison.OrdinalIgnoreCase))
                    {
                        options.WebOutputDirectory = arg.Substring("--web=".Length);
                    }
                }

                return options;
            }

            private static bool ReadValue(string[] args, ref int index, string name, out string value)
            {
                var arg = args[index];
                value = string.Empty;

                if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(name.Length + 1);
                    return true;
                }

                if (!arg.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (index + 1 >= args.Length)
                    return false;

                value = args[++index];
                return true;
            }

            private static bool TryParseMode(string value, out ExecutionMode mode)
            {
                if (Enum.TryParse(value, ignoreCase: true, out mode))
                    return true;

                mode = ExecutionMode.Instant;
                return false;
            }
        }
    }
}
