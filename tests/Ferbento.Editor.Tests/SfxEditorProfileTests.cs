using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Editor;
using Ferbento.Editor.Sfx;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Tests;

public class SfxEditorProfileTests
{
    [Fact]
    public void SetProfile_Pico8_ReportsSlotAndStepCounts()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());

        Assert.Equal(64, ctrl.SfxSlotCount);
        Assert.Equal(32, ctrl.NotesPerSfx);
        Assert.Equal(4, ctrl.ChannelCount);
    }

    [Fact]
    public void SetProfile_GameBoy_32Slots()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.GameBoy());

        Assert.Equal(32, ctrl.SfxSlotCount);
        ctrl.SelectedSlot = 99;
        Assert.Equal(31, ctrl.SelectedSlot);
    }

    [Fact]
    public void SetProfile_CustomNotesPerSfx_ResizesStepsAndClampsLoop()
    {
        var profile = BuiltInProfiles.Snes();
        profile.Audio.NotesPerSfx = 64;

        var ctrl = new SfxEditorController();
        ctrl.SetProfile(profile);
        ctrl.LoopEnd = 99;

        Assert.Equal(64, ctrl.NotesPerSfx);
        Assert.Equal(63, ctrl.LoopEnd);
        Assert.Equal(64, ctrl.Document.Notes.Length);
    }

    [Fact]
    public void SetProfile_Snes_8Channels()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Snes());

        Assert.Equal(8, ctrl.ChannelCount);
        ctrl.SelectedChannel = 7;
        Assert.Equal(7, ctrl.SelectedChannel);
        ctrl.SelectedChannel = 8;
        Assert.Equal(7, ctrl.SelectedChannel);
    }

    [Fact]
    public void SetChipBus_Pico8_LoadsSlotWithinProfileBounds()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var notes = new SfxNote[32];
        notes[0] = new SfxNote(60, 1, 7);
        bus.Sound.SetSlot(0, notes, 4, 0, 31);

        var ctrl = new SfxEditorController();
        ctrl.SetChipBus(bus);

        Assert.False(ctrl.Document.GetNote(0).IsEmpty);
        Assert.Equal(60, ctrl.Document.GetNote(0).Pitch);
    }

    [Fact]
    public void SetProfile_PreservesSlotNamesWithinNewBounds()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SlotName = "kick";

        var gameBoy = BuiltInProfiles.GameBoy();
        ctrl.SetProfile(gameBoy);

        Assert.Equal("kick", ctrl.SlotName);
        Assert.Equal(32, ctrl.SfxSlotCount);
    }

    [Fact]
    public void SetProfile_ReclampsSlotAndChannel_OnProfileChange()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SelectedSlot = 50;
        ctrl.SelectedChannel = 3;

        ctrl.SetProfile(BuiltInProfiles.GameBoy());

        Assert.Equal(31, ctrl.SelectedSlot);
        Assert.Equal(3, ctrl.SelectedChannel);
        Assert.Equal(32, ctrl.SfxSlotCount);
    }

    [Fact]
    public void EditorSession_ProfileChange_UpdatesBusLimits()
    {
        var session = new EditorSession(BuiltInProfiles.Pico8());
        var smaller = BuiltInProfiles.Pico8();
        smaller.Audio.SfxSlots = 16;

        session.ApplyProfile(smaller);

        Assert.Equal(16, session.Profile.Audio.SfxSlots);
        Assert.Equal(16, session.Bus.Profile.Audio.SfxSlots);
    }

    [Fact]
    public void SetProfile_CustomOctaveRange_UpdatesPianoRollRange()
    {
        var profile = BuiltInProfiles.Pico8();
        profile.Audio.SfxOctaveMin = 2;
        profile.Audio.SfxOctaveMax = 4;

        var ctrl = new SfxEditorController();
        ctrl.SetProfile(profile);

        Assert.Equal(2, SfxPitch.MinOctaveFromView());
        Assert.Equal(4, SfxPitch.MaxOctaveFromView());
    }

    [Fact]
    public void SlotColor_DefaultsBySlotIndex()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());

        Assert.Equal(SfxSlotColors.DefaultIndex(0), ctrl.GetSlotColor(0));
        Assert.Equal(SfxSlotColors.DefaultIndex(3), ctrl.GetSlotColor(3));
    }

    [Fact]
    public void SlotColor_ExportImportRoundTrip()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SelectedSlot = 5;
        ctrl.SlotColor = 9;
        ctrl.SlotName = "kick";

        var data = ctrl.ExportProjectData();
        var other = new SfxEditorController();
        other.SetProfile(BuiltInProfiles.Pico8());
        other.ImportProjectData(data);

        Assert.Equal(9, other.GetSlotColor(5));
        other.SelectedSlot = 5;
        Assert.Equal("kick", other.SlotName);
    }

    [Fact]
    public void CustomInst_PlaceNoteAndExport_RoundTrip()
    {
        var ctrl = new SfxEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.DefaultCustomInst = true;
        ctrl.DefaultWaveform = 3;
        ctrl.PlaceNote(0, 20, preview: false);

        var note = ctrl.GetNote(0);
        Assert.True(note.CustomInst);
        Assert.Equal(3, note.Instrument);

        var data = ctrl.ExportProjectData();
        data.WaveformInstrumentSlots = [false, false, true, false, false, false, false, false];
        var other = new SfxEditorController();
        other.SetProfile(BuiltInProfiles.Pico8());
        other.ImportProjectData(data);
        Assert.True(other.GetWaveformInstrumentSlot(2));
    }

    [Fact]
    public void AudioEditorLimits_FromProfile_MatchesMemoryLayout()
    {
        var limits = AudioEditorLimits.FromProfile(BuiltInProfiles.Nes());

        Assert.Equal(32, limits.NotesPerSfx);
        Assert.Equal(32, limits.SfxSlotCount);
        Assert.Equal(5, limits.ChannelCount);
    }
}
