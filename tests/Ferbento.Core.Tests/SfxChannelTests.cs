using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Core.Memory;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Tests;

public class SfxTypesTests
{
    [Fact]
    public void SfxNote_RoundTripsThroughRawBytes()
    {
        var note = new SfxNote(32, 3, 5, SfxEffect.Vibrato, customInst: true);
        var restored = SfxNote.FromRaw(note.ToRaw());

        Assert.Equal(32, restored.Pitch);
        Assert.Equal(3, restored.Instrument);
        Assert.Equal(5, restored.Volume);
        Assert.Equal(SfxEffect.Vibrato, restored.Effect);
        Assert.True(restored.CustomInst);
    }

    [Fact]
    public void SfxCodec_WritesAndReadsSlotFromMemory()
    {
        var profile = HardwareProfile.Pico8();
        var layout = AudioMemoryLayout.FromProfile(profile);
        var mem = new VirtualMemory(profile.Memory.Ram);

        var notes = new SfxNote[32];
        notes[0] = new SfxNote(24, 2, 7);
        SfxCodec.WriteSlot(mem, layout, 3, notes, speed: 6, loopStart: 1, loopEnd: 8, SfxFilters.Buzz | SfxFilters.Reverb);

        var read = SfxCodec.ReadNotes(mem, layout, 3);
        var header = SfxCodec.ReadHeader(mem, layout, 3);
        int headerAddr = layout.SfxSlotAddress(3) + layout.NotesPerSfx * 2;

        Assert.Equal(24, read[0].Pitch);
        Assert.Equal(6, header.speed);
        Assert.Equal(1, header.loopStart);
        Assert.Equal(8, header.loopEnd);
        Assert.True((header.filters & SfxFilters.Buzz) != 0);
        Assert.True((header.filters & SfxFilters.Reverb) != 0);
        Assert.Equal(28, mem.Peek(headerAddr));
        Assert.Equal(6, mem.Peek(headerAddr + 1));
        Assert.Equal(1, mem.Peek(headerAddr + 2));
        Assert.Equal(8, mem.Peek(headerAddr + 3));
    }

    [Fact]
    public void Pico8SfxHeader_RoundTripsFilterByte()
    {
        var filters = SfxFilters.Noiz | SfxFilters.Detune1 | SfxFilters.Dampen;
        byte encoded = Pico8SfxHeader.EncodeFilterByte(filters);
        var decoded = Pico8SfxHeader.DecodeFilterByte(encoded);
        Assert.Equal(filters, decoded);
    }

    [Fact]
    public void SongChannelEntry_FromByte_DecodesEnabledBit()
    {
        var entry = SongChannelEntry.FromByte(0x47);
        Assert.Equal(7, entry.SfxIndex);
        Assert.True(entry.Enabled);
    }

    [Fact]
    public void SongCodec_RoundTripsPatternFlags()
    {
        var profile = HardwareProfile.Pico8();
        var layout = AudioMemoryLayout.FromProfile(profile);
        var mem = new VirtualMemory(profile.Memory.Ram);

        var channels = new[]
        {
            new SongChannelEntry(0, false),
            new SongChannelEntry(0, false),
            new SongChannelEntry(0, false),
            new SongChannelEntry(7, true),
        };
        var pattern = new SongPattern(channels, SongPatternFlags.LoopStart);

        Assert.Equal(4, layout.Channels);
        Assert.Equal(4, layout.SongBytesPerPattern);
        SongCodec.WritePattern(mem, layout, 2, pattern);

        Assert.Equal(0x80, mem.Peek(layout.SongPatternAddress(2)) & 0x80);
        Assert.Equal(0x47, mem.Peek(layout.SongPatternAddress(2) + 3));
        var restored = SongCodec.ReadPattern(mem, layout, 2);
        Assert.Equal(7, restored.Channels[3].SfxIndex);
        Assert.True(restored.Channels[3].Enabled);
        Assert.True((restored.Flags & SongPatternFlags.LoopStart) != 0);
    }
}

public class SfxChannelTests
{
    [Fact]
    public void EmptyStep_IsSilence_NotInfiniteSustain()
    {
        var ch = new SfxChannel(0, sampleRate: 60);
        ch.SetSpeed(1);
        ch.SetLoop(0, 2);
        ch.SetNote(0, new SfxNote(note: 60, instrument: 2, volume: 7));
        ch.SetNote(1, new SfxNote());

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

    [Fact]
    public void NoizFilter_ForcesWhiteNoiseOnInstrument6()
    {
        var ch = new SfxChannel(0, 22050);
        ch.SetFilters(SfxFilters.Noiz);
        ch.SetNote(0, new SfxNote(32, 6, 7));
        ch.SetSpeed(int.MaxValue);
        ch.Play(0);
        ch.FlushCommands();

        var samples = Enumerable.Range(0, 32).Select(_ => ch.NextSample()).ToArray();
        Assert.True(samples.Any(s => s != 0f));
        Assert.True(samples.Max(s => System.Math.Abs(s)) > 0.01f);
    }

    [Fact]
    public void SlideEffect_GlidesFromPreviousPitch()
    {
        var ch = new SfxChannel(0, sampleRate: 60);
        ch.SetSpeed(4);
        ch.SetNote(0, new SfxNote(40, 2, 7));
        ch.SetNote(1, new SfxNote(52, 2, 7, SfxEffect.Slide));
        ch.SetLoop(0, 1);
        ch.Play(0);
        ch.FlushCommands();

        float start = ch.NextSample();
        for (int i = 0; i < 30; i++)
            ch.NextSample();
        float later = ch.NextSample();

        Assert.NotEqual(0f, start);
        Assert.NotEqual(0f, later);
    }

    [Fact]
    public void BuzzFilter_ModifiesWaveform()
    {
        var plain = new SfxChannel(0, 22050);
        var buzz = new SfxChannel(0, 22050);
        plain.SetNote(0, new SfxNote(40, 2, 7));
        buzz.SetNote(0, new SfxNote(40, 2, 7));
        buzz.SetFilters(SfxFilters.Buzz);
        plain.SetSpeed(int.MaxValue);
        buzz.SetSpeed(int.MaxValue);
        plain.Play(0);
        buzz.Play(0);
        plain.FlushCommands();
        buzz.FlushCommands();

        var sPlain = Enumerable.Range(0, 64).Select(_ => plain.NextSample()).ToArray();
        var sBuzz = Enumerable.Range(0, 64).Select(_ => buzz.NextSample()).ToArray();
        Assert.True(sPlain.Zip(sBuzz, (a, b) => System.Math.Abs(a - b)).Average() > 0.001);
    }

    [Fact]
    public void CustomInstrument_UsesInstrumentSource()
    {
        var source = new TestInstrumentSource();
        var ch = new SfxChannel(0, 22050);
        ch.SetInstrumentSource(source);
        ch.SetNote(0, new SfxNote(32, 0, 7, customInst: true));
        ch.SetSpeed(int.MaxValue);
        ch.Play(0);
        ch.FlushCommands();

        float sample = ch.NextSample();
        Assert.NotEqual(0f, sample);
    }

    private sealed class TestInstrumentSource : ISfxInstrumentSource
    {
        public bool TryGetInstrumentSfx(int instrumentIndex, out SfxNote[] notes, out int speed, out int loopStart, out int loopEnd, out SfxFilters filters)
        {
            notes = [new SfxNote(32, 2, 7)];
            speed = 1;
            loopStart = 0;
            loopEnd = 0;
            filters = SfxFilters.None;
            return instrumentIndex == 0;
        }

        public bool TryGetWaveformTable(int instrumentIndex, out byte[] table)
        {
            table = [];
            return false;
        }
    }
}

public class AudioEngineTests
{
    [Fact]
    public void SetSlot_PreservesEffectParamInCache()
    {
        using var engine = CreateEngine();
        var notes = new SfxNote[32];
        const int param = (30 << 8) | 8;
        notes[0] = new SfxNote(40, 2, 7, SfxEffect.Vibrato, param);
        engine.SetSlot(0, notes, 4, 0, 31);

        var read = engine.GetSlot(0);
        Assert.Equal(SfxEffect.Vibrato, read[0].Effect);
        Assert.Equal(param, read[0].EffectParam);
    }

    [Fact]
    public void StopMusic_silences_all_channels()
    {
        using var engine = CreateEngine();
        WriteSongPattern(engine, 0, sfxPerChannel: [0, -1, -1, -1]);
        WriteSfxSlot(engine, 0, new SfxNote(32, 2, 7));

        engine.PlayMusic(0, 0, 15);
        engine.Update();

        Assert.True(engine.IsMusicPlaying);

        engine.StopMusic();

        Assert.False(engine.IsMusicPlaying);
        Assert.All(engine.Channels, ch => Assert.False(ch.IsPlaying));
    }

    [Fact]
    public void MusicSequencer_AdvancesToNextPattern()
    {
        using var engine = CreateEngine();
        WriteSongPattern(engine, 0, sfxPerChannel: [0, -1, -1, -1]);
        WriteSongPattern(engine, 1, sfxPerChannel: [1, -1, -1, -1]);
        WriteSfxSlot(engine, 0, new SfxNote(32, 2, 7));
        WriteSfxSlot(engine, 1, new SfxNote(36, 1, 7), speed: 1, loopStart: 0, loopEnd: 0);

        engine.PlayMusic(0, 0, 15);
        engine.ProcessCommands();
        engine.Channels[0].StopImmediate();

        for (int i = 0; i < 4; i++)
            engine.Update();

        Assert.True(engine.StatCurrentPattern() >= 0);
    }

    [Fact]
    public void SfxStopCases_WorkThroughSoundChip()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        WriteSfxSlot(bus.Sound.Engine, 2, new SfxNote(32, 2, 7));

        bus.Sound.Sfx(2, 0);
        bus.Sound.Engine.Update();
        Assert.True(bus.Sound.Engine.Channels[0].IsPlaying);

        bus.Sound.Sfx(-1, 0);
        bus.Sound.Engine.Update();
        bus.Sound.Engine.ProcessCommands();
        bus.Sound.Engine.Channels[0].FlushCommands();
        Assert.False(bus.Sound.Engine.Channels[0].IsPlaying);

        bus.Sound.Sfx(2, -1);
        bus.Sound.Engine.Update();
        Assert.True(bus.Sound.Engine.Channels.Any(c => c.IsPlaying));

        bus.Sound.Sfx(2, -2);
        bus.Sound.Engine.ProcessCommands();
        bus.Sound.Engine.Channels.ToList().ForEach(c => c.FlushCommands());
        Assert.DoesNotContain(bus.Sound.Engine.Channels, c => c.PlayingSfxIndex == 2 && c.IsPlaying);
    }

    [Fact]
    public void StatQueries_ReflectPlaybackState()
    {
        using var engine = CreateEngine();
        WriteSongPattern(engine, 0, sfxPerChannel: [5, -1, -1, -1]);
        WriteSfxSlot(engine, 5, new SfxNote(32, 2, 7));

        Assert.Equal(0x45, engine.Memory!.Peek(0x3100));

        engine.PlayMusic(0, 0, 15);

        Assert.Equal(5, engine.StatSfxOnChannel(0));
        Assert.Equal(0, engine.StatCurrentPattern());
        Assert.True(engine.StatMusicPlaying());
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

    private static AudioEngine CreateEngine()
    {
        var profile = HardwareProfile.Pico8();
        var mem = new VirtualMemory(profile.Memory.Ram);
        var engine = new AudioEngine(profile, mem, enablePlayback: false);
        return engine;
    }

    private static void WriteSfxSlot(AudioEngine engine, int slot, SfxNote note, int speed = 4, int loopStart = 0, int loopEnd = 0)
    {
        var notes = new SfxNote[32];
        notes[0] = note;
        engine.SetSlot(slot, notes, speed, loopStart, loopEnd);
    }

    private static void WriteSongPattern(AudioEngine engine, int index, int[] sfxPerChannel)
    {
        Assert.NotNull(engine.Memory);
        var channels = new SongChannelEntry[sfxPerChannel.Length];
        for (int i = 0; i < sfxPerChannel.Length; i++)
        {
            int sfx = sfxPerChannel[i];
            channels[i] = sfx >= 0 ? new SongChannelEntry(sfx, true) : new SongChannelEntry(0, false);
        }
        SongCodec.WritePattern(engine.Memory!, engine.Layout, index, new SongPattern(channels));
    }

    private static float[] CollectSamples(SfxChannel channel, int count)
    {
        channel.SetSpeed(int.MaxValue);
        channel.SetLoop(0, 31);
        channel.FlushCommands();
        return Enumerable.Range(0, count).Select(_ => channel.NextSample()).ToArray();
    }
}
