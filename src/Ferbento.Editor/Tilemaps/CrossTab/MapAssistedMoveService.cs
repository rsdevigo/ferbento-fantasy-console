using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.Tilemaps.CrossTab;

namespace Ferbento.Editor.Tilemaps.CrossTab;

public sealed class MapAssistedMoveService
{
    private MapAssistedMoveState? _state;

    public bool HasCutPending => _state?.IsCutPending == true;

    public string StatusHint => _state?.IsCutPending == true
        ? "Step 3/4: select map region, pick dest tile, Ctrl+V"
        : "";

    public bool BeginCut(SpriteEditorController spriteCtrl, IEnumerable<int> spriteIndices)
    {
        var indices = spriteIndices.OrderBy(i => i).ToArray();
        if (indices.Length == 0) return false;

        var doc = spriteCtrl.Document;
        var tile = doc.TileSize;
        var minCol = indices.Min(i => i % doc.SpritesPerRow);
        var maxCol = indices.Max(i => i % doc.SpritesPerRow);
        var minRow = indices.Min(i => i / doc.SpritesPerRow);
        var maxRow = indices.Max(i => i / doc.SpritesPerRow);
        var ox = minCol * tile;
        var oy = minRow * tile;
        var w = (maxCol - minCol + 1) * tile;
        var h = (maxRow - minRow + 1) * tile;
        var pixels = doc.ReadRegion(ox, oy, w, h);
        var cleared = new byte[pixels.Length];

        spriteCtrl.UndoService.Execute(new SheetRegionCommand(doc, ox, oy, w, h, pixels, cleared));

        _state = new MapAssistedMoveState
        {
            SourceSpriteIndices = indices,
            SpritePixelData = pixels,
            SpriteRegionWidth = w,
            SpriteRegionHeight = h,
            OriginSheetX = ox,
            OriginSheetY = oy,
            IsCutPending = true,
        };
        spriteCtrl.RequestRefresh();
        return true;
    }

    public bool ApplyPaste(
        ChipBus bus,
        TilemapEditorController mapCtrl,
        SpriteEditorController spriteCtrl,
        int destSpriteIndex)
    {
        if (_state is not { IsCutPending: true, SpritePixelData: { } pixels }) return false;
        if (mapCtrl.Selection.MapRect is not { } region) return false;

        var doc = spriteCtrl.Document;
        var tile = doc.TileSize;
        var destCol = destSpriteIndex % doc.SpritesPerRow;
        var destRow = destSpriteIndex / doc.SpritesPerRow;
        var destX = destCol * tile;
        var destY = destRow * tile;

        var mapBefore = mapCtrl.Document.ReadRegion(region.x, region.y, region.w, region.h);
        var cutBefore = doc.ReadRegion(_state.OriginSheetX, _state.OriginSheetY, _state.SpriteRegionWidth, _state.SpriteRegionHeight);
        var destBefore = doc.ReadRegion(destX, destY, _state.SpriteRegionWidth, _state.SpriteRegionHeight);

        var indexSet = _state.SourceSpriteIndices.ToHashSet();
        var originMinIndex = _state.SourceSpriteIndices.Min();
        var originCol = originMinIndex % doc.SpritesPerRow;
        var originRow = originMinIndex / doc.SpritesPerRow;

        var mapAfter = (byte[])mapBefore.Clone();
        for (var row = 0; row < region.h; row++)
        for (var col = 0; col < region.w; col++)
        {
            var mx = region.x + col;
            var my = region.y + row;
            if (mx < 0 || my < 0 || mx >= mapCtrl.MapWidth || my >= mapCtrl.MapHeight) continue;
            var oldId = bus.Tilemap.MGet(mx, my);
            if (!indexSet.Contains(oldId)) continue;
            var oldCol = oldId % doc.SpritesPerRow;
            var oldRow = oldId / doc.SpritesPerRow;
            var relCol = oldCol - originCol;
            var relRow = oldRow - originRow;
            var newId = (destRow + relRow) * doc.SpritesPerRow + (destCol + relCol);
            if (newId >= 0 && newId < doc.SpriteCount)
                mapAfter[row * region.w + col] = (byte)newId;
        }

        var cutAfter = new byte[cutBefore.Length];
        var destAfter = (byte[])pixels.Clone();

        var composite = new CrossTabMoveCommand(
            mapCtrl.Document, region.x, region.y, region.w, region.h, mapBefore, mapAfter, mapCtrl.CurrentLayer,
            doc,
            _state.OriginSheetX, _state.OriginSheetY, _state.SpriteRegionWidth, _state.SpriteRegionHeight,
            cutBefore, cutAfter,
            destX, destY, destBefore, destAfter);

        mapCtrl.UndoService.Execute(composite);
        spriteCtrl.UndoService.PushExecuted(composite);

        _state = null;
        mapCtrl.NotifyEdited();
        spriteCtrl.NotifyEdited();
        mapCtrl.CrossTabHint = "";
        mapCtrl.RequestRefresh();
        spriteCtrl.RequestRefresh();
        return true;
    }

    public void Cancel() => _state = null;
}

public sealed class MapAssistedMoveState
{
    public int[] SourceSpriteIndices { get; init; } = [];
    public byte[]? SpritePixelData { get; init; }
    public int SpriteRegionWidth { get; init; }
    public int SpriteRegionHeight { get; init; }
    public int OriginSheetX { get; init; }
    public int OriginSheetY { get; init; }
    public bool IsCutPending { get; init; }
}
