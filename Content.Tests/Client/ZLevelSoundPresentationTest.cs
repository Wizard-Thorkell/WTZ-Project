using Content.Client.ZLevel;
using NUnit.Framework;
using Robust.Shared.Audio;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ZLevelSoundPresentationTest
{
    [Test]
    public void LinearRouteGainReplacesPortalDistanceAndAppliesTransmission()
    {
        var multiplier = ZLevelSoundPresentationSystem.GetRouteGainMultiplier(
            Attenuation.LinearDistanceClamped,
            2f,
            6f,
            1f,
            1f,
            10f,
            0.5f);

        Assert.That(multiplier, Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void NoAttenuationUsesOnlyRouteTransmission()
    {
        var multiplier = ZLevelSoundPresentationSystem.GetRouteGainMultiplier(
            Attenuation.NoAttenuation,
            0f,
            8f,
            1f,
            1f,
            10f,
            0.35f);

        Assert.That(multiplier, Is.EqualTo(0.35f).Within(0.0001f));
    }

    [Test]
    public void UnclampedInverseDistanceSupportsPortalAtListener()
    {
        var multiplier = ZLevelSoundPresentationSystem.GetRouteGainMultiplier(
            Attenuation.InverseDistance,
            0f,
            2f,
            1f,
            1f,
            10f,
            0.5f);

        Assert.That(multiplier, Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void InvalidOrFullyAttenuatedRoutesFailClosed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ZLevelSoundPresentationSystem.GetRouteGainMultiplier(
                    Attenuation.LinearDistanceClamped,
                    2f,
                    6f,
                    1f,
                    1f,
                    10f,
                    float.NaN),
                Is.Zero);
            Assert.That(
                ZLevelSoundPresentationSystem.GetRouteGainMultiplier(
                    Attenuation.LinearDistance,
                    10f,
                    10f,
                    1f,
                    1f,
                    2f,
                    1f),
                Is.Zero);
        });
    }
}
