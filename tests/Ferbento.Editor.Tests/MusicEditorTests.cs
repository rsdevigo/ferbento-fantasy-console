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
}
