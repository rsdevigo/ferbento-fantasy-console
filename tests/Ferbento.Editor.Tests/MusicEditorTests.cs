using Ferbento.Core.Chips;
using Ferbento.Editor.Music;
using Ferbento.Editor.Music.Input;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Tests;

public class MusicEditorTests
{
    private static MusicEditorController CreateController(HardwareProfile? profile = null)
    {
        profile ??= BuiltInProfiles.Pico8();
        var ctrl = new MusicEditorController();
        ctrl.SetChipBus(new ChipBus(profile));
        return ctrl;
    }

    [Fact]
    public void ProfileAware_Pico8_HasFourChannels()
    {
        var ctrl = CreateController(BuiltInProfiles.Pico8());
        Assert.Equal(4, ctrl.ChannelCount);
        Assert.Equal(64, ctrl.PatternCount);
    }

    [Fact]
    public void ProfileAware_CustomProfile_ClampsChannelsAndPatterns()
    {
        var profile = BuiltInProfiles.Pico8();
        profile.Audio.Channels = 2;
        profile.Audio.MusicPatterns = 8;
        var ctrl = CreateController(profile);
        Assert.Equal(2, ctrl.ChannelCount);
        Assert.Equal(8, ctrl.PatternCount);
        ctrl.CurrentPattern = 99;
        Assert.Equal(7, ctrl.CurrentPattern);
    }

    [Fact]
    public void MusicNoteCodec_PackUnpack_RoundTrip()
    {
        int packed = MusicNoteCodec.Pack(60, 3, 5, Core.Audio.SfxEffect.Vibrato, 42);
        var (note, inst, vol, fx, param) = MusicNoteCodec.Unpack(packed);
        Assert.Equal(60, note);
        Assert.Equal(3, inst);
        Assert.Equal(5, vol);
        Assert.Equal(Core.Audio.SfxEffect.Vibrato, fx);
        Assert.Equal(42, param);
    }

    [Fact]
    public void PlaceNote_WritesCellAndAutoIncrements()
    {
        var ctrl = CreateController();
        ctrl.AutoIncrement = 1;
        ctrl.SelectedRow = 0;
        ctrl.SelectedChannel = 0;
        ctrl.PlaceNoteAtSelection(48);
        Assert.True(ctrl.HasNoteAt(0, 0));
        Assert.Equal(1, ctrl.SelectedRow);
    }

    [Fact]
    public void Undo_RestoresClearedCell()
    {
        var ctrl = CreateController();
        ctrl.AutoIncrement = 0;
        ctrl.PlaceNoteAtSelection(48);
        ctrl.ClearSelectedCell();
        Assert.False(ctrl.HasNoteAt(0, 0));
        ctrl.Undo.Undo();
        Assert.True(ctrl.HasNoteAt(0, 0));
    }

    [Fact]
    public void ExportImportProjectData_PreservesPattern()
    {
        var ctrl = CreateController();
        ctrl.PlaceNoteAtSelection(55);
        var exported = ctrl.ExportProjectData();

        var ctrl2 = CreateController();
        ctrl2.ImportProjectData(exported);
        Assert.True(ctrl2.HasNoteAt(0, 0));
        var (note, _, _, _, _) = ctrl2.Document.GetCellDecoded(0, 0, 0);
        Assert.Equal(55, note);
    }

    [Fact]
    public void MusicEditorInputMap_TempoPlusMinus()
    {
        Assert.True(MusicEditorInputMap.Matches("OemPlus", false, false, false, MusicEditorAction.TempoUp));
        Assert.True(MusicEditorInputMap.Matches("OemMinus", false, false, false, MusicEditorAction.TempoDown));
    }

    [Fact]
    public void MusicDocument_CellIndex_ScalesWithChannelCount()
    {
        var doc = new MusicDocument();
        var profile = BuiltInProfiles.Pico8();
        profile.Audio.Channels = 2;
        doc.SetProfile(profile);
        Assert.Equal(5, doc.GetCellIndex(2, 1));
    }

    [Fact]
    public void MusicPatternGenerator_RespectsLengthAndChannels()
    {
        var data = MusicPatternGenerator.Generate(16, 4, 0, 7, new Random(42));
        Assert.Equal(64, data.Length);

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == 0) continue;
            var (_, inst, vol, _, _) = MusicNoteCodec.Unpack(data[i]);
            Assert.InRange(inst, 0, 7);
            Assert.InRange(vol, 1, 7);
        }
    }

    [Fact]
    public void MusicPatternGenerator_LeavesRests()
    {
        var data = MusicPatternGenerator.Generate(32, 4, 0, 7, new Random(123));
        int filled = data.Count(v => v != 0);
        Assert.True(filled > 0);
        Assert.True(filled < data.Length);
    }

    [Fact]
    public void ToggleCell_KeepsSelectionOnEditedCell()
    {
        var ctrl = CreateController();
        ctrl.AutoIncrement = 1;
        ctrl.SelectedRow = 0;
        ctrl.SelectedChannel = 0;

        ctrl.ToggleCell(5, 2);
        Assert.Equal(5, ctrl.SelectedRow);
        Assert.Equal(2, ctrl.SelectedChannel);
        Assert.True(ctrl.HasNoteAt(5, 2));

        ctrl.ToggleCell(5, 2);
        Assert.Equal(5, ctrl.SelectedRow);
        Assert.Equal(2, ctrl.SelectedChannel);
        Assert.False(ctrl.HasNoteAt(5, 2));
    }

    [Fact]
    public void RandomGeneratePattern_FillsCurrentPattern()
    {
        var ctrl = CreateController();
        ctrl.Length = 16;
        ctrl.RandomGeneratePattern();

        int notes = 0;
        for (int r = 0; r < ctrl.Length; r++)
            for (int c = 0; c < ctrl.ChannelCount; c++)
                if (ctrl.HasNoteAt(r, c)) notes++;

        Assert.True(notes > 0);
        Assert.Contains("pattern", ctrl.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClampedPlayheadRow_WhenNotPlaying_IsNegativeOne()
    {
        var ctrl = CreateController();
        Assert.Equal(-1, ctrl.ClampedPlayheadRow);
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(7, 8, 7)]
    [InlineData(12, 8, 7)]
    [InlineData(-1, 8, -1)]
    public void ClampPlayheadRow_ClampsToPatternLength(int playhead, int length, int expected)
    {
        Assert.Equal(expected, MusicEditorController.ClampPlayheadRow(playhead, length));
    }

    [Fact]
    public void ClampInstrument_LimitsToBuiltInWaveforms()
    {
        var doc = new MusicDocument();
        Assert.Equal(7, doc.ClampInstrument(99));
        Assert.Equal(0, doc.ClampInstrument(-1));
    }
}
