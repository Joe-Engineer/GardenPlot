// <copyright file="UnhandledErrorRecorder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services;

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Diagnostic capture for unhandled exceptions that bypass the routed
/// <c>&lt;ErrorBoundary&gt;</c> (event-handler async, JS interop, fire-and-forget
/// tasks, WASM runtime). Records the last N exceptions in a bounded ring buffer
/// and writes them through <see cref="ILogger"/> so the browser DevTools console
/// always carries the next reproduction's stack trace.
/// </summary>
/// <remarks>
/// <para>
/// Filed under issue #214 — "An unhandled error has occurred. Reload." surfaced
/// during the 2026-06-03 demo with no diagnostic data, blocking root-cause
/// analysis. This recorder is the data-capture half of the fix; a follow-up will
/// surface the buffer in an in-page diagnostics panel for non-developer users.
/// </para>
/// <para>
/// Thread-safe via <see cref="ConcurrentQueue{T}"/>; safe to call from any
/// hook (<see cref="System.AppDomain.UnhandledException"/>,
/// <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/>,
/// or any code path that catches an exception it can't otherwise surface).
/// </para>
/// </remarks>
public sealed class UnhandledErrorRecorder
{
    /// <summary>Maximum recorded errors kept in memory; oldest are dropped on overflow.</summary>
    public const int MaxCapacity = 50;

    private readonly ConcurrentQueue<RecordedError> errors = new();
    private readonly ILogger<UnhandledErrorRecorder>? logger;

    /// <summary>Initializes a new instance with optional logger forwarding.</summary>
    /// <param name="logger">Optional ILogger. Recorded errors are also written here at <see cref="LogLevel.Error"/>.</param>
    public UnhandledErrorRecorder(ILogger<UnhandledErrorRecorder>? logger = null)
    {
        this.logger = logger;
    }

    /// <summary>Snapshot of recorded errors, newest last.</summary>
    public IReadOnlyList<RecordedError> RecentErrors => this.errors.ToArray();

    /// <summary>Current count of recorded errors, capped at <see cref="MaxCapacity"/>.</summary>
    public int Count => this.errors.Count;

    /// <summary>
    /// Records an unhandled exception with a free-form context tag (e.g. the source
    /// hook name, the active tool, the in-progress action). Truncates the buffer to
    /// <see cref="MaxCapacity"/> by dropping the oldest entries.
    /// </summary>
    /// <param name="exception">The exception to record. Null is a no-op.</param>
    /// <param name="context">A short, human-readable context tag. May be null.</param>
    public void Record(Exception? exception, string? context = null)
    {
        if (exception is null)
        {
            return;
        }

        RecordedError record = new(
            UtcTimestamp: DateTimeOffset.UtcNow,
            Context: context ?? string.Empty,
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            Message: exception.Message ?? string.Empty,
            StackTrace: exception.StackTrace ?? string.Empty);

        this.errors.Enqueue(record);
        while (this.errors.Count > MaxCapacity && this.errors.TryDequeue(out _))
        {
            // Trim until at-or-below capacity. Concurrent-safe; competing producers
            // will leave the queue at <= MaxCapacity + N (N = concurrent producers)
            // momentarily, which is acceptable for a diagnostic buffer.
        }

        // Always write to the logger so the next-occurrence trace lands in the
        // browser console even before any in-page diagnostics UI exists.
        if (this.logger is not null)
        {
            this.logger.LogError(
                exception,
                "[UnhandledErrorRecorder] {Context} :: {ExceptionType}: {Message}",
                record.Context,
                record.ExceptionType,
                record.Message);
        }
    }

    /// <summary>Clears all recorded errors. Intended for tests and the "clear log" UI affordance.</summary>
    public void Clear()
    {
        while (this.errors.TryDequeue(out _))
        {
            // drain
        }
    }

    /// <summary>
    /// Formats the recorded errors as a copy-pasteable diagnostic block for the
    /// browser console or a future "copy to clipboard" UI action.
    /// </summary>
    /// <returns>A multi-line string; empty when no errors are recorded.</returns>
    public string FormatForDiagnostics()
    {
        RecordedError[] snapshot = this.errors.ToArray();
        if (snapshot.Length == 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Garden Plot — {snapshot.Length} recorded unhandled error(s)");
        for (int i = 0; i < snapshot.Length; i++)
        {
            RecordedError e = snapshot[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"--- [{i + 1}/{snapshot.Length}] {e.UtcTimestamp:u} ---");
            if (!string.IsNullOrEmpty(e.Context))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"Context: {e.Context}");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"{e.ExceptionType}: {e.Message}");
            if (!string.IsNullOrEmpty(e.StackTrace))
            {
                sb.AppendLine(e.StackTrace);
            }
        }

        return sb.ToString();
    }
}

/// <summary>One captured unhandled exception with timestamp and optional context.</summary>
/// <param name="UtcTimestamp">When the error was recorded, UTC.</param>
/// <param name="Context">Free-form context tag from the call site (may be empty).</param>
/// <param name="ExceptionType">Full type name of the exception.</param>
/// <param name="Message">Exception message.</param>
/// <param name="StackTrace">Stack trace text (may be empty for thrown-without-throw exceptions).</param>
public sealed record RecordedError(
    DateTimeOffset UtcTimestamp,
    string Context,
    string ExceptionType,
    string Message,
    string StackTrace);
