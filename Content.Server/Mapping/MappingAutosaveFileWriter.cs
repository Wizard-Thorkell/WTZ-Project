// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.IO;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Mapping;

/// <summary>
/// Writes one new autosave through a same-directory temporary file, then
/// promotes it without replacing an existing destination.
/// </summary>
internal static class MappingAutosaveFileWriter
{
    internal static bool TryWrite(
        IWritableDirProvider directory,
        ResPath destination,
        ReadOnlyMemory<byte> data,
        out string error,
        Action<Stream, ReadOnlyMemory<byte>>? writeTemporary = null,
        Func<ResPath, ResPath>? temporaryPathFactory = null)
    {
        error = string.Empty;
        if (!destination.IsRooted)
        {
            error = "The autosave destination must be rooted.";
            return false;
        }

        if (directory.Exists(destination))
        {
            error = $"The autosave destination '{destination}' already exists.";
            return false;
        }

        var temporary = temporaryPathFactory?.Invoke(destination) ??
                        destination.Directory / $".{destination.Filename}.{Guid.NewGuid():N}.tmp";
        if (!temporary.IsRooted || temporary.Directory != destination.Directory || temporary == destination)
        {
            error = "The autosave temporary file must be a distinct rooted path in the destination directory.";
            return false;
        }

        var createdTemporary = false;
        try
        {
            using (var stream = directory.Open(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                createdTemporary = true;
                if (writeTemporary == null)
                    stream.Write(data.Span);
                else
                    writeTemporary(stream, data);

                stream.Flush();
                if (stream is FileStream fileStream)
                    fileStream.Flush(flushToDisk: true);
            }

            directory.Rename(temporary, destination);
            return true;
        }
        catch (Exception operationException)
        {
            if (!createdTemporary)
            {
                error = operationException.Message;
                return false;
            }

            try
            {
                directory.Delete(temporary);
                error = operationException.Message;
            }
            catch (Exception cleanupException)
            {
                error = "Autosave writing failed and its temporary file could not be removed: " +
                        $"{operationException.Message} Cleanup error: {cleanupException.Message}";
            }

            return false;
        }
    }
}
