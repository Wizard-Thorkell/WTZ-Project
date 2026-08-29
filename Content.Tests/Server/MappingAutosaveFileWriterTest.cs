// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.IO;
using System.Text;
using Content.Server.Mapping;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Tests.Server;

[TestFixture]
public sealed class MappingAutosaveFileWriterTest
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    [Test]
    public void PromotesCompleteTemporaryFileWithoutBom()
    {
        var directory = CreateDirectory();
        var destination = new ResPath("/Autosaves/map.yml");
        var temporary = new ResPath("/Autosaves/.map.yml.fixed.tmp");
        var text = "name: t\u00E9rreo\nlabel: \u5730\u4E0B\n";
        var data = StrictUtf8.GetBytes(text);

        Assert.That(MappingAutosaveFileWriter.TryWrite(
            directory,
            destination,
            data,
            out var error,
            temporaryPathFactory: _ => temporary), Is.True, error);

        var written = directory.ReadAllBytes(destination);
        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(data));
            Assert.That(written.AsSpan().StartsWith(Encoding.UTF8.Preamble), Is.False);
            Assert.That(StrictUtf8.GetString(written), Is.EqualTo(text));
            Assert.That(directory.Exists(temporary), Is.False);
        });
    }

    [Test]
    public void RemovesPartialTemporaryFileAfterWriteFailure()
    {
        var directory = CreateDirectory();
        var destination = new ResPath("/Autosaves/map.yml");
        var temporary = new ResPath("/Autosaves/.map.yml.fixed.tmp");
        var data = StrictUtf8.GetBytes("meta:\n  format: 7\n");

        Assert.That(MappingAutosaveFileWriter.TryWrite(
            directory,
            destination,
            data,
            out var error,
            (stream, bytes) =>
            {
                stream.Write(bytes.Span[..4]);
                throw new IOException("Injected temporary write failure.");
            },
            _ => temporary), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(error, Does.Contain("Injected temporary write failure"));
            Assert.That(directory.Exists(destination), Is.False);
            Assert.That(directory.Exists(temporary), Is.False);
            Assert.That(directory.DirectoryEntries(new ResPath("/Autosaves")), Is.Empty);
        });
    }

    [Test]
    public void PreservesExistingDestinationAndUnownedTemporaryFile()
    {
        var directory = CreateDirectory();
        var destination = new ResPath("/Autosaves/map.yml");
        var temporary = new ResPath("/Autosaves/.map.yml.fixed.tmp");
        var original = StrictUtf8.GetBytes("original\n");
        var unownedTemporary = StrictUtf8.GetBytes("other writer\n");
        directory.WriteAllBytes(destination, original);
        directory.WriteAllBytes(temporary, unownedTemporary);

        Assert.That(MappingAutosaveFileWriter.TryWrite(
            directory,
            destination,
            StrictUtf8.GetBytes("replacement\n"),
            out var destinationError,
            temporaryPathFactory: _ => temporary), Is.False);
        Assert.That(directory.ReadAllBytes(destination), Is.EqualTo(original));

        directory.Delete(destination);
        Assert.That(MappingAutosaveFileWriter.TryWrite(
            directory,
            destination,
            StrictUtf8.GetBytes("replacement\n"),
            out var temporaryError,
            temporaryPathFactory: _ => temporary), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(destinationError, Does.Contain("already exists"));
            Assert.That(temporaryError, Does.Contain("already exists"));
            Assert.That(directory.Exists(destination), Is.False);
            Assert.That(directory.ReadAllBytes(temporary), Is.EqualTo(unownedTemporary),
                "A failed CreateNew must not delete a temporary file owned by another writer.");
        });
    }

    [Test]
    public void ChoosesSuffixWhenTimestampDestinationAlreadyExists()
    {
        var directory = CreateDirectory();
        var saveDirectory = new ResPath("/Autosaves");
        var timestamp = new DateTime(2026, 8, 29, 14, 3, 27, 456, DateTimeKind.Local);
        var occupied = saveDirectory / "2026-08-29_14.03.27.456-AUTO.yml";
        directory.WriteAllBytes(occupied, StrictUtf8.GetBytes("existing\n"));

        var available = MappingSystem.GetAvailableAutosavePath(directory, saveDirectory, timestamp);

        Assert.Multiple(() =>
        {
            Assert.That(available,
                Is.EqualTo(saveDirectory / "2026-08-29_14.03.27.456-AUTO-1.yml"));
            Assert.That(directory.ReadAllText(occupied), Is.EqualTo("existing\n"));
            Assert.That(directory.Exists(available), Is.False);
        });
    }

    private static VirtualWritableDirProvider CreateDirectory()
    {
        var directory = new VirtualWritableDirProvider();
        directory.CreateDir(new ResPath("/Autosaves"));
        return directory;
    }
}
