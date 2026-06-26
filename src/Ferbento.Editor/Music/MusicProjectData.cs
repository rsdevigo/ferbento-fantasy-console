namespace Ferbento.Editor.Music;

public sealed class MusicProjectData
{
    public string SongName { get; set; } = "Untitled";
    public int CurrentPattern { get; set; }
    public int Length { get; set; } = 32;
    public int Tempo { get; set; } = 8;
    public int AutoIncrement { get; set; } = 1;
    public int LoopStart { get; set; }
    public int LoopEnd { get; set; } = 63;
    public int[] Sequence { get; set; } = [];
    public string[]? PatternDataBase64 { get; set; }
}
