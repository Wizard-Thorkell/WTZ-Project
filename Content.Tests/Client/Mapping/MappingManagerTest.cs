using System;
using System.Text;
using Content.Client.Mapping;
using NUnit.Framework;

namespace Content.Tests.Client.Mapping;

[TestFixture]
public sealed class MappingManagerTest
{
    [Test]
    public void EncodesSnapshotAsStrictUtf8WithoutBom()
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var yml = "name: t\u00E9rreo\nlabel: \u5730\u4E0B\n";

        var encoded = MappingManager.EncodeSnapshot(yml);

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Is.EqualTo(encoding.GetBytes(yml)));
            Assert.That(encoded.AsSpan().StartsWith(Encoding.UTF8.Preamble), Is.False);
            Assert.That(encoding.GetString(encoded), Is.EqualTo(yml));
        });
    }

    [Test]
    public void RejectsInvalidUtf16BeforeEncodingSnapshot()
    {
        Assert.That(
            () => MappingManager.EncodeSnapshot("name: \uD800\n"),
            Throws.ArgumentException);
    }
}
