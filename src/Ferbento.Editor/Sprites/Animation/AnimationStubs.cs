namespace Ferbento.Editor.Sprites.Animation;

public sealed class SpriteFrame
{
    public int SpriteIndex { get; init; }
    public int Flags { get; init; }
}

public sealed class SpriteFrameSequence
{
    public List<SpriteFrame> Frames { get; } = [];
    public int Fps { get; set; } = 8;
}

public sealed class OnionSkinSettings
{
    public bool Enabled { get; set; }
    public int PrevFrames { get; set; } = 1;
    public int NextFrames { get; set; } = 1;
    public double Opacity { get; set; } = 0.4;
}

public sealed class TimelineState
{
    public int CurrentFrame { get; set; }
    public int FrameCount { get; set; }
}

public interface IAnimationProvider
{
    int GetFrameCount();
    SpriteFrame? GetFrame(int index);
    void SetCurrentFrame(int index);
}

public sealed class NullAnimationProvider : IAnimationProvider
{
    public int GetFrameCount() => 0;
    public SpriteFrame? GetFrame(int index) => null;
    public void SetCurrentFrame(int index) { }
}
