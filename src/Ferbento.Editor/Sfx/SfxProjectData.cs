namespace Ferbento.Editor.Sfx;

/// <summary>Editor-only SFX metadata (not stored in Pico-8 cartridge RAM).</summary>
public sealed class SfxProjectData
{
    public string[]? SlotNames { get; set; }
    public int[]? SlotColors { get; set; }
    /// <summary>When true, SFX slots 0–7 define custom waveform tables (Pico-8 instrument slots).</summary>
    public bool[]? WaveformInstrumentSlots { get; set; }
}
