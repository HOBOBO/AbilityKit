using System.Text;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class MemoryPackExportVerifierTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "abilitykit-export-verifier-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Verify_PassesWhenCommittedFilesMatchThePlan()
    {
        Write("Payload.MemoryPack.g.cs", "content-a\r\n");
        Write("ProtocolCatalogs.g.cs", "content-b\r\n");
        Write("protocol-export.json", "content-c\r\n");

        var verification = Verify(
            ("Payload.MemoryPack.g.cs", "content-a\r\n"),
            ("ProtocolCatalogs.g.cs", "content-b\r\n"),
            ("protocol-export.json", "content-c\r\n"));

        Assert.True(verification.IsCurrent);
        Assert.Empty(verification.Mismatches);
    }

    [Fact]
    public void Verify_IgnoresLineEndingDifferences()
    {
        Write("Payload.MemoryPack.g.cs", "line1\r\nline2\r\n");

        var verification = Verify(("Payload.MemoryPack.g.cs", "line1\nline2\n"));

        Assert.True(verification.IsCurrent);
    }

    [Fact]
    public void Verify_ReportsMissingFiles()
    {
        var verification = Verify(
            ("Payload.MemoryPack.g.cs", "content"),
            ("protocol-export.json", "{}"));

        Assert.False(verification.IsCurrent);
        Assert.Equal(2, verification.Mismatches.Count);
        Assert.All(verification.Mismatches,
            mismatch => Assert.Equal(MemoryPackExportMismatchKind.MissingFile, mismatch.Kind));
    }

    [Fact]
    public void Verify_ReportsDriftedContentAsStale()
    {
        Write("Payload.MemoryPack.g.cs", "committed\r\ncontent");

        var verification = Verify(("Payload.MemoryPack.g.cs", "deterministic\ncontent"));

        var mismatch = Assert.Single(verification.Mismatches);
        Assert.Equal(MemoryPackExportMismatchKind.StaleFile, mismatch.Kind);
        Assert.Equal("Payload.MemoryPack.g.cs", mismatch.FileName);
    }

    [Fact]
    public void Verify_ReportsStaleGeneratedFilesThatAreNoLongerPlanned()
    {
        Write("Payload.MemoryPack.g.cs", "content\r\n");
        Write("RemovedPayload.MemoryPack.g.cs", "old content");
        Write("CaseVariation.MemoryPack.g.cs", "case variation");

        var verification = Verify(("Payload.MemoryPack.g.cs", "content\n"));

        Assert.False(verification.IsCurrent);
        Assert.Equal(2, verification.Mismatches.Count);
        Assert.All(verification.Mismatches,
            mismatch => Assert.Equal(MemoryPackExportMismatchKind.ExtraFile, mismatch.Kind));
    }

    [Fact]
    public void Verify_IgnoresUnityMetaAndUnrelatedFiles()
    {
        Write("Payload.MemoryPack.g.cs", "content");
        Write("Payload.MemoryPack.g.cs.meta", "fileFormatVersion: 2");
        Write("notes.txt", "not managed by the export");

        var verification = Verify(("Payload.MemoryPack.g.cs", "content"));

        Assert.True(verification.IsCurrent);
    }

    [Fact]
    public void Verify_ReportsMissingFilesWhenExportFolderDoesNotExist()
    {
        var missingDirectory = Path.Combine(_root, "missing");

        var verification = MemoryPackExportVerifier.Verify(
            missingDirectory,
            Plan(("Payload.MemoryPack.g.cs", "content")));

        Assert.False(verification.IsCurrent);
        Assert.All(verification.Mismatches,
            mismatch => Assert.Equal(MemoryPackExportMismatchKind.MissingFile, mismatch.Kind));
    }

    [Fact]
    public void IsManagedFile_MatchesGeneratedSourcesAndManifestOnly()
    {
        Assert.True(MemoryPackExportVerifier.IsManagedFile("Payload.MemoryPack.g.cs"));
        Assert.True(MemoryPackExportVerifier.IsManagedFile("payload.memorypack.g.cs"));
        Assert.True(MemoryPackExportVerifier.IsManagedFile("protocol-export.json"));
        Assert.False(MemoryPackExportVerifier.IsManagedFile("Payload.MemoryPack.g.cs.meta"));
        Assert.False(MemoryPackExportVerifier.IsManagedFile("Something.cs"));
        Assert.False(MemoryPackExportVerifier.IsManagedFile("notes.txt"));
    }

    private MemoryPackExportVerification Verify(params (string Name, string Content)[] planned) =>
        MemoryPackExportVerifier.Verify(_root, Plan(planned));

    private static Dictionary<string, string> Plan(params (string Name, string Content)[] planned)
    {
        var plan = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, content) in planned) plan[name] = content;
        return plan;
    }

    private void Write(string name, string content)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, name),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
