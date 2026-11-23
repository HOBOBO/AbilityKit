using AbilityKit.Ability.StateSync.Samples;
using Xunit;
using StateSample = AbilityKit.Ability.StateSync.Samples.LocalStateCommitExample.StateSample;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalStateCommitExampleTests
{
    [Fact]
    public void FirstImportFailure_DoesNotCreateCursor_AndFrameZeroCanBeRetried()
    {
        var host = new LocalStateCommitExample();
        var candidate = new StateSample(frame: 0, revision: 1, value: 10);

        Assert.False(host.TryCommit(candidate, RejectImport));
        Assert.False(host.TryGetCommitted(out _));

        Assert.True(host.TryCommit(candidate, AcceptImport));
        AssertCommitted(host, frame: 0, revision: 1, value: 10);
    }

    [Fact]
    public void FailedImport_PreservesLastCommit_AndSameRevisionCanBeRetried()
    {
        var host = new LocalStateCommitExample();
        Assert.True(host.TryCommit(new StateSample(2, 4, 10), AcceptImport));
        var candidate = new StateSample(3, 5, 20);

        Assert.False(host.TryCommit(candidate, RejectAfterWritingCandidate));
        AssertCommitted(host, frame: 2, revision: 4, value: 10);

        Assert.True(host.TryCommit(candidate, ImportAdjustedValue));
        AssertCommitted(host, frame: 3, revision: 5, value: 21);

        bool RejectAfterWritingCandidate(int candidateValue, out int importedValue)
        {
            importedValue = candidateValue + 1;
            AssertCommitted(host, frame: 2, revision: 4, value: 10);
            return false;
        }

        static bool ImportAdjustedValue(int candidateValue, out int importedValue)
        {
            importedValue = candidateValue + 1;
            return true;
        }
    }

    [Theory]
    [InlineData(-1, 5L)]
    [InlineData(3, 0L)]
    [InlineData(3, -1L)]
    public void InvalidCandidate_DoesNotInvokeImporterOnEmptyHost(int frame, long revision)
    {
        var host = new LocalStateCommitExample();

        Assert.False(host.TryCommit(new StateSample(frame, revision, 20), UnexpectedImport));
        Assert.False(host.TryGetCommitted(out _));
    }

    [Theory]
    [InlineData(2, 4L)]
    [InlineData(3, 4L)]
    [InlineData(3, 3L)]
    [InlineData(1, 5L)]
    public void DuplicateOrLateCandidate_DoesNotInvokeImporter(int frame, long revision)
    {
        var host = new LocalStateCommitExample();
        Assert.True(host.TryCommit(new StateSample(2, 4, 10), AcceptImport));

        Assert.False(host.TryCommit(new StateSample(frame, revision, 20), UnexpectedImport));
        AssertCommitted(host, frame: 2, revision: 4, value: 10);
    }

    [Fact]
    public void SameFrame_AllowsNewerRevision()
    {
        var host = new LocalStateCommitExample();
        Assert.True(host.TryCommit(new StateSample(0, 1, 10), AcceptImport));

        Assert.True(host.TryCommit(new StateSample(0, 3, 20), AcceptImport));
        AssertCommitted(host, frame: 0, revision: 3, value: 20);
    }

    [Fact]
    public void ImportException_PropagatesAndLeavesCommitUnchanged()
    {
        var host = new LocalStateCommitExample();
        Assert.True(host.TryCommit(new StateSample(2, 4, 10), AcceptImport));
        var candidate = new StateSample(3, 5, 20);
        var failure = new InvalidOperationException("导入中断");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => host.TryCommit(candidate, ThrowOnImport)));
        AssertCommitted(host, frame: 2, revision: 4, value: 10);
        Assert.True(host.TryCommit(candidate, AcceptImport));

        bool ThrowOnImport(int candidateValue, out int importedValue) => throw failure;
    }

    [Fact]
    public void ReentrantImport_CannotPublishAnotherCommit()
    {
        var host = new LocalStateCommitExample();
        Assert.True(host.TryCommit(new StateSample(2, 4, 10), AcceptImport));
        var candidate = new StateSample(3, 5, 20);

        Assert.Throws<InvalidOperationException>(() => host.TryCommit(candidate, Reenter));
        AssertCommitted(host, frame: 2, revision: 4, value: 10);
        Assert.True(host.TryCommit(candidate, AcceptImport));

        bool Reenter(int candidateValue, out int importedValue)
        {
            importedValue = candidateValue;
            return host.TryCommit(new StateSample(4, 6, 30), UnexpectedImport);
        }
    }

    private static void AssertCommitted(LocalStateCommitExample host, int frame, long revision, int value)
    {
        Assert.True(host.TryGetCommitted(out var committed));
        Assert.Equal(frame, committed.Frame);
        Assert.Equal(revision, committed.Revision);
        Assert.Equal(value, committed.Value);
    }

    private static bool AcceptImport(int candidateValue, out int importedValue)
    {
        importedValue = candidateValue;
        return true;
    }

    private static bool RejectImport(int candidateValue, out int importedValue)
    {
        importedValue = candidateValue;
        return false;
    }

    private static bool UnexpectedImport(int candidateValue, out int importedValue) =>
        throw new Xunit.Sdk.XunitException("不应调用导入。");
}
