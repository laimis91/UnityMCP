#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UnityMcp.Editor
{

internal static class UnityMcpConsoleLogBuffer
{
    private const int DefaultCapacity = 2000;

    private static readonly object Sync = new();

    private static readonly List<ConsoleLogEntry> Entries = new(DefaultCapacity);
    private static bool _initialized;
    private static long _nextSequence;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= OnLogMessageReceivedThreaded;
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
            _initialized = true;
        }
    }

    public static ConsoleLogQueryResult GetSnapshot(int maxResults, bool includeStackTrace, IReadOnlyCollection<string>? levels = null)
    {
        EnsureInitialized();
        return QueryInternal(afterSequence: null, maxResults, includeStackTrace, levels);
    }

    public static ConsoleLogQueryResult GetTail(long afterSequence, int maxResults, bool includeStackTrace, IReadOnlyCollection<string>? levels = null)
    {
        EnsureInitialized();
        return QueryInternal(afterSequence, maxResults, includeStackTrace, levels);
    }

    private static void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
    {
        var entry = new ConsoleLogEntry(
            sequence: Interlocked.Increment(ref _nextSequence),
            timestampUtc: DateTimeOffset.UtcNow,
            logType: type.ToString(),
            level: MapLevel(type),
            message: condition ?? string.Empty,
            stackTrace: stackTrace ?? string.Empty);

        lock (Sync)
        {
            if (Entries.Count >= DefaultCapacity)
            {
                Entries.RemoveAt(0);
            }

            Entries.Add(entry);
        }
    }

    private static ConsoleLogQueryResult QueryInternal(long? afterSequence, int maxResults, bool includeStackTrace, IReadOnlyCollection<string>? levels)
    {
        List<ConsoleLogEntry> selected = new();
        long bufferStartSequence;
        long latestSequence;
        int totalBuffered;
        bool truncated;
        bool cursorBehindBuffer;

        lock (Sync)
        {
            if (Entries.Count == 0)
            {
                bufferStartSequence = 0;
                latestSequence = Volatile.Read(ref _nextSequence);
                totalBuffered = 0;
                truncated = false;
                cursorBehindBuffer = false;
            }
            else
            {
                bufferStartSequence = Entries[0].Sequence;
                latestSequence = Entries[^1].Sequence;
                totalBuffered = Entries.Count;

                var startIndex = 0;
                if (afterSequence.HasValue)
                {
                    var cursor = afterSequence.Value;
                    cursorBehindBuffer = cursor < (bufferStartSequence - 1);

                    while (startIndex < Entries.Count && Entries[startIndex].Sequence <= cursor)
                    {
                        startIndex++;
                    }
                }
                else
                {
                    cursorBehindBuffer = false;
                    startIndex = Math.Max(0, Entries.Count - maxResults);
                }

                var availableCount = Entries.Count - startIndex;
                truncated = false;

                if (levels is null || levels.Count == 0)
                {
                    var takeCount = Math.Min(maxResults, Math.Max(0, availableCount));
                    truncated = availableCount > takeCount;

                    for (var index = 0; index < takeCount; index++)
                    {
                        selected.Add(Entries[startIndex + index]);
                    }
                }
                else
                {
                    var levelSet = new HashSet<string>(levels, StringComparer.OrdinalIgnoreCase);
                    for (var index = startIndex; index < Entries.Count; index++)
                    {
                        var entry = Entries[index];
                        if (!levelSet.Contains(entry.Level))
                        {
                            continue;
                        }

                        if (selected.Count < maxResults)
                        {
                            selected.Add(entry);
                        }
                        else
                        {
                            truncated = true;
                            break;
                        }
                    }
                }
            }
        }

        var nextAfterSequence = afterSequence ?? 0;
        if (selected.Count > 0)
        {
            nextAfterSequence = selected[^1].Sequence;
        }
        else if (!afterSequence.HasValue)
        {
            nextAfterSequence = latestSequence;
        }

        var items = new List<object>(selected.Count);
        foreach (var entry in selected)
        {
            items.Add(new
            {
                sequence = entry.Sequence,
                timestampUtc = entry.TimestampUtc.ToString("O"),
                logType = entry.LogType,
                level = entry.Level,
                message = entry.Message,
                stackTrace = includeStackTrace && !string.IsNullOrWhiteSpace(entry.StackTrace) ? entry.StackTrace : null
            });
        }

        return new ConsoleLogQueryResult(
            bufferCapacity: DefaultCapacity,
            totalBuffered: totalBuffered,
            bufferStartSequence,
            latestSequence,
            afterSequence,
            nextAfterSequence,
            cursorBehindBuffer,
            truncated,
            includeStackTrace,
            items);
    }

    private static string MapLevel(LogType logType)
    {
        return logType switch
        {
            LogType.Warning => "warning",
            LogType.Error => "error",
            LogType.Assert => "assert",
            LogType.Exception => "exception",
            _ => "info"
        };
    }

    private sealed class ConsoleLogEntry
    {
        public ConsoleLogEntry(
            long sequence,
            DateTimeOffset timestampUtc,
            string logType,
            string level,
            string message,
            string stackTrace)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            LogType = logType;
            Level = level;
            Message = message;
            StackTrace = stackTrace;
        }

        public long Sequence { get; }

        public DateTimeOffset TimestampUtc { get; }

        public string LogType { get; }

        public string Level { get; }

        public string Message { get; }

        public string StackTrace { get; }
    }

    internal sealed class ConsoleLogQueryResult
    {
        public ConsoleLogQueryResult(
            int bufferCapacity,
            int totalBuffered,
            long bufferStartSequence,
            long latestSequence,
            long? afterSequence,
            long nextAfterSequence,
            bool cursorBehindBuffer,
            bool truncated,
            bool includeStackTrace,
            IReadOnlyList<object> items)
        {
            BufferCapacity = bufferCapacity;
            TotalBuffered = totalBuffered;
            BufferStartSequence = bufferStartSequence;
            LatestSequence = latestSequence;
            AfterSequence = afterSequence;
            NextAfterSequence = nextAfterSequence;
            CursorBehindBuffer = cursorBehindBuffer;
            Truncated = truncated;
            IncludeStackTrace = includeStackTrace;
            Items = items;
        }

        public int BufferCapacity { get; }

        public int TotalBuffered { get; }

        public long BufferStartSequence { get; }

        public long LatestSequence { get; }

        public long? AfterSequence { get; }

        public long NextAfterSequence { get; }

        public bool CursorBehindBuffer { get; }

        public bool Truncated { get; }

        public bool IncludeStackTrace { get; }

        public IReadOnlyList<object> Items { get; }
    }
}
}
