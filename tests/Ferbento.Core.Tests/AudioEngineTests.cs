using Ferbento.Core.Audio;

namespace Ferbento.Core.Tests;

public class AudioEngineTests
{
    [Fact]
    public void StopMusic_silences_all_channels()
    {
        using var engine = new AudioEngine(22050, 4, 64);
        var pattern = new int[256];
        pattern[0] = 0x0000_3C05; // note 60, instrument 0, volume 5
        engine.SetMusicPattern(0, pattern);
        engine.PlayMusic(0, 0, 15);
        engine.Update();

        Assert.True(engine.IsMusicPlaying);

        engine.StopMusic();

        Assert.False(engine.IsMusicPlaying);
        Assert.All(engine.Channels, ch => Assert.False(ch.IsPlaying));
    }

    private static float[] CollectSamples(SfxChannel channel, int count)
    {
        channel.SetSpeed(int.MaxValue);
        channel.SetLoop(0, 31);
        channel.FlushCommands();
        return Enumerable.Range(0, count).Select(_ => channel.NextSample()).ToArray();
    }

    [Fact]
    public void Instrument11_MatchesMelodicWaveform_NotNoise()
    {
        var ch11 = new SfxChannel(0, 22050);
        var ch2 = new SfxChannel(0, 22050);
        var ch3 = new SfxChannel(0, 22050);

        ch11.SetNote(0, new SfxNote(60, 11, 7));
        ch2.SetNote(0, new SfxNote(60, 2, 7));
        ch3.SetNote(0, new SfxNote(60, 3, 7));
        ch11.Play(0);
        ch2.Play(0);
        ch3.Play(0);

        var s11 = CollectSamples(ch11, 64);
        var s2 = CollectSamples(ch2, 64);
        var s3 = CollectSamples(ch3, 64);

        for (int i = 0; i < 64; i++)
            Assert.Equal(s2[i], s11[i], precision: 5);

        Assert.True(s3.Zip(s11, (a, b) => System.Math.Abs(a - b)).Average() > 0.01);
    }

    [Fact]
    public void Instrument15_MatchesMelodicWaveform()
    {
        var ch15 = new SfxChannel(0, 22050);
        var ch0 = new SfxChannel(0, 22050);

        ch15.SetNote(0, new SfxNote(60, 15, 7));
        ch0.SetNote(0, new SfxNote(60, 0, 7));
        ch15.Play(0);
        ch0.Play(0);

        var s15 = CollectSamples(ch15, 64);
        var s0 = CollectSamples(ch0, 64);

        for (int i = 0; i < 64; i++)
            Assert.Equal(s0[i], s15[i], precision: 5);
    }
}
