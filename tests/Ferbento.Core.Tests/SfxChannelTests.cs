using Ferbento.Core.Audio;

namespace Ferbento.Core.Tests;

public class SfxChannelTests
{
    [Fact]
    public void EmptyStep_IsSilence_NotInfiniteSustain()
    {
        var ch = new SfxChannel(0, sampleRate: 60);
        ch.SetSpeed(1);
        ch.SetLoop(0, 2);
        ch.SetNote(0, new SfxNote(note: 60, instrument: 2, volume: 7));
        ch.SetNote(1, new SfxNote()); // rest

        ch.Play(0);
        ch.FlushCommands();

        float first = ch.NextSample();
        float second = ch.NextSample();

        Assert.NotEqual(0f, first);
        Assert.Equal(0f, second);
    }

    [Fact]
    public void SingleStepLoop_StopsAfterFirstStep()
    {
        var ch = new SfxChannel(0, sampleRate: 60);
        ch.SetSpeed(1);
        ch.SetLoop(0, 0);
        ch.SetNote(0, new SfxNote(note: 60, instrument: 2, volume: 7));
        ch.SetNote(1, new SfxNote(note: 72, instrument: 2, volume: 7));

        ch.Play(0);
        ch.FlushCommands();

        float first = ch.NextSample();
        float second = ch.NextSample();

        Assert.NotEqual(0f, first);
        Assert.Equal(0f, second);
        Assert.False(ch.IsPlaying);
    }
}
