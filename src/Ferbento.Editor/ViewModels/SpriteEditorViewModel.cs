using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.ViewModels;

public sealed class SpriteEditorViewModel : INotifyPropertyChanged
{
    public SpriteEditorController Controller { get; } = new();

    public event Action? Edited
    {
        add => Controller.Edited += value;
        remove => Controller.Edited -= value;
    }

    public bool CanUndo => Controller.CanUndo;
    public bool CanRedo => Controller.CanRedo;
    public int TileSize => Controller.TileSize;
    public int SheetWidth => Controller.SheetWidth;
    public int SheetHeight => Controller.SheetHeight;
    public int SpritesPerRow => Controller.SpritesPerRow;
    public int SpriteCount => Controller.SpriteCount;
    public int PaletteCount => Controller.PaletteCount;
    public bool UsesGfx2Bank => Controller.UsesGfx2Bank;
    public int SelectedColor { get => Controller.SelectedColor; set => Controller.SelectedColor = value; }
    public int PrimaryColor => Controller.PrimaryColor;
    public int SecondaryColor => Controller.SecondaryColor;
    public string SelectedTool { get => Controller.SelectedTool; set => Controller.SelectedTool = value; }
    public int CurrentSpriteIndex { get => Controller.CurrentSpriteIndex; set => Controller.CurrentSpriteIndex = value; }
    public int GfxBank => Controller.GfxBank;
    public string SpriteLabel => Controller.SpriteLabel;
    public string StatusText => Controller.StatusText;
    public int AllFlags => Controller.AllFlags;
    public ColorChip? ColorChip => Controller.ColorChip;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SpriteEditorViewModel()
    {
        Controller.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                OnPropertyChanged(e.PropertyName);
        };
        Controller.RefreshRequested += () => OnPropertyChanged(nameof(StatusText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void SetProfile(HardwareProfile profile) => Controller.SetProfile(profile);
    public void SetChipBus(ChipBus bus) => Controller.SetChipBus(bus);
    public void SwitchGfxBank(int bank) => Controller.SwitchGfxBank(bank);
    public (int sheetX, int sheetY) SpriteOrigin => Controller.SpriteOrigin;
    public int GetFlag(int bit) => Controller.GetFlag(bit);
    public void SetFlag(int bit, bool value) => Controller.SetFlag(bit, value);
    public void ToggleFlag(int bit) => Controller.ToggleFlag(bit);
    public void Undo() => Controller.HandleKey("Z", ctrl: true, shift: false, alt: false);
    public void Redo() => Controller.HandleKey("Y", ctrl: true, shift: false, alt: false);
    public int GetPixel(int localX, int localY) => Controller.GetPixel(localX, localY);
    public int ReadSheetPixel(int sheetX, int sheetY) => Controller.ReadSheetPixel(sheetX, sheetY);
    public void FlipHorizontal() => Controller.FlipHorizontal();
    public void FlipVertical() => Controller.FlipVertical();
    public void Rotate90() => Controller.Rotate90();
    public void CopySprite() => Controller.Copy();
    public void PasteSprite() => Controller.Paste();
    public void FillSprite(int color) => Controller.ClearSprite();
}
