namespace Ferbento.Editor.Sfx;

public static class SfxPitch
{
  public static IReadOnlyList<string> PitchNames { get; } = GeneratePitchNames();
  public static IReadOnlyList<int> PitchMidi { get; } = GeneratePitchMidi();

  public static string NoteToPitchName(int midiNote)
  {
    if (midiNote <= 0) return "--";
    string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    int octave = (midiNote / 12) - 1;
    int n = midiNote % 12;
    return $"{notes[n]}{octave}";
  }

  public static int RowToMidi(int row)
  {
    if (row < 0 || row >= PitchMidi.Count) return 60;
    return PitchMidi[row];
  }

  public static int MidiToRow(int midi)
  {
    for (int i = 0; i < PitchMidi.Count; i++)
    {
      if (PitchMidi[i] == midi) return i;
    }

    int best = 0;
    int bestDist = int.MaxValue;
    for (int i = 0; i < PitchMidi.Count; i++)
    {
      int dist = Math.Abs(PitchMidi[i] - midi);
      if (dist < bestDist)
      {
        bestDist = dist;
        best = i;
      }
    }

    return best;
  }

  private static List<string> GeneratePitchNames()
  {
    var names = new List<string>();
    string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    for (int octave = 4; octave <= 8; octave++)
    {
      for (int n = 0; n < 12; n++)
      {
        int midiNote = (octave + 1) * 12 + n;
        if (midiNote < 48 || midiNote > 108) continue;
        names.Add($"{notes[n]}{octave}");
      }
    }

    return names;
  }

  private static List<int> GeneratePitchMidi()
  {
    var midi = new List<int>();
    for (int octave = 4; octave <= 8; octave++)
    {
      for (int n = 0; n < 12; n++)
      {
        int midiNote = (octave + 1) * 12 + n;
        if (midiNote < 48 || midiNote > 108) continue;
        midi.Add(midiNote);
      }
    }

    return midi;
  }
}
