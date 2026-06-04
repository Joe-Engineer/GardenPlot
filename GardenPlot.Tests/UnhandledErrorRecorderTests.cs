// <copyright file="UnhandledErrorRecorderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Services;

/// <summary>
/// Unit tests for <see cref="UnhandledErrorRecorder"/> (issue #214). The recorder is
/// a singleton diagnostic ring buffer; tests cover the contract used by Program.cs's
/// global hooks (record / capacity / clear / formatting).
/// </summary>
public class UnhandledErrorRecorderTests
{
    [Fact]
    public void Record_NullException_IsNoOp()
    {
        UnhandledErrorRecorder recorder = new();

        recorder.Record(null, context: "anywhere");

        Assert.Equal(0, recorder.Count);
        Assert.Empty(recorder.RecentErrors);
    }

    [Fact]
    public void Record_CapturesExceptionTypeMessageAndContext()
    {
        UnhandledErrorRecorder recorder = new();
        InvalidOperationException ex = new("test boom");

        recorder.Record(ex, context: "unit-test");

        RecordedError captured = Assert.Single(recorder.RecentErrors);
        Assert.Equal("System.InvalidOperationException", captured.ExceptionType);
        Assert.Equal("test boom", captured.Message);
        Assert.Equal("unit-test", captured.Context);
        Assert.True(captured.UtcTimestamp <= DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.True(captured.UtcTimestamp >= DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Record_NullContext_StoresEmptyString()
    {
        UnhandledErrorRecorder recorder = new();

        recorder.Record(new InvalidOperationException("x"), context: null);

        Assert.Equal(string.Empty, recorder.RecentErrors[0].Context);
    }

    [Fact]
    public void Record_BeyondCapacity_DropsOldestEntries()
    {
        UnhandledErrorRecorder recorder = new();

        for (int i = 0; i < UnhandledErrorRecorder.MaxCapacity + 10; i++)
        {
            recorder.Record(new InvalidOperationException($"err-{i}"), context: $"ctx-{i}");
        }

        Assert.Equal(UnhandledErrorRecorder.MaxCapacity, recorder.Count);

        // Oldest 10 were dropped; the snapshot should start at err-10 (zero-indexed),
        // i.e. the first surviving entry's message is "err-10".
        IReadOnlyList<RecordedError> snapshot = recorder.RecentErrors;
        Assert.Equal("err-10", snapshot[0].Message);
        Assert.Equal($"err-{UnhandledErrorRecorder.MaxCapacity + 9}", snapshot[^1].Message);
    }

    [Fact]
    public void Clear_EmptiesTheBuffer()
    {
        UnhandledErrorRecorder recorder = new();
        recorder.Record(new InvalidOperationException("one"));
        recorder.Record(new InvalidOperationException("two"));
        Assert.Equal(2, recorder.Count);

        recorder.Clear();

        Assert.Equal(0, recorder.Count);
        Assert.Empty(recorder.RecentErrors);
    }

    [Fact]
    public void FormatForDiagnostics_EmptyBuffer_ReturnsEmptyString()
    {
        UnhandledErrorRecorder recorder = new();

        Assert.Equal(string.Empty, recorder.FormatForDiagnostics());
    }

    [Fact]
    public void FormatForDiagnostics_IncludesAllRecords_WithTypeAndContext()
    {
        UnhandledErrorRecorder recorder = new();
        recorder.Record(new InvalidOperationException("first failure"), context: "stamp-drop");
        recorder.Record(new ArgumentException("second failure"), context: "indexeddb-save");

        string formatted = recorder.FormatForDiagnostics();

        Assert.Contains("2 recorded unhandled error(s)", formatted, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException", formatted, StringComparison.Ordinal);
        Assert.Contains("first failure", formatted, StringComparison.Ordinal);
        Assert.Contains("Context: stamp-drop", formatted, StringComparison.Ordinal);
        Assert.Contains("System.ArgumentException", formatted, StringComparison.Ordinal);
        Assert.Contains("second failure", formatted, StringComparison.Ordinal);
        Assert.Contains("Context: indexeddb-save", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatForDiagnostics_OmitsEmptyContextLine()
    {
        UnhandledErrorRecorder recorder = new();
        recorder.Record(new InvalidOperationException("no-context"), context: null);

        string formatted = recorder.FormatForDiagnostics();

        Assert.Contains("no-context", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("Context: ", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_PreservesOrderAcrossMixedCalls()
    {
        UnhandledErrorRecorder recorder = new();

        recorder.Record(new InvalidOperationException("a"), context: "x");
        recorder.Record(new InvalidOperationException("b"), context: "y");
        recorder.Record(new InvalidOperationException("c"), context: "z");

        IReadOnlyList<RecordedError> snapshot = recorder.RecentErrors;
        Assert.Equal(3, snapshot.Count);
        Assert.Equal("a", snapshot[0].Message);
        Assert.Equal("b", snapshot[1].Message);
        Assert.Equal("c", snapshot[2].Message);
    }
}
