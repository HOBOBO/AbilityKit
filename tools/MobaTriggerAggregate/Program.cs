using AbilityKit.Triggering.Runtime.Plan.Json;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: MobaTriggerAggregate <trigger-directory> <output-json>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var sources = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
    .Select(path => new TriggerPlanAggregateCompiler.SourceDocument(
        Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)));
var output = TriggerPlanAggregateCompiler.Compile(sources);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], output);
Console.WriteLine($"Compiled trigger aggregate from {Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Length} source files.");
return 0;
