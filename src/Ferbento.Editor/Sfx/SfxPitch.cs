namespace Ferbento.Editor.Sfx;

/// <summary>
/// Pico-8 SFX pitch is 0–63 (stored in 6 bits). The piano roll shows a scrollable window into that range.
/// </summary>
public static class SfxPitch
{
  public const int PicoMin = 0;
  public const int PicoMax = 63;

  private static int _viewMin;
  private static int _viewMax = PicoMax;
  private static List<string> _pitchNames = GeneratePitchNames(_viewMin, _viewMax);
  private static List<int> _pitchValues = GeneratePitchValues(_viewMin, _viewMax);

  public static IReadOnlyList<string> PitchNames => _pitchNames;
  public static IReadOnlyList<int> PitchMidi => _pitchValues;
  public static int ViewMin => _viewMin;
  public static int ViewMax => _viewMax;

  public static void SetViewRange(int minPitch, int maxPitch)
  {
    minPitch = Math.Clamp(minPitch, PicoMin, PicoMax - 1);
    maxPitch = Math.Clamp(maxPitch, minPitch + 1, PicoMax);
    if (minPitch == _viewMin && maxPitch == _viewMax) return;
    _viewMin = minPitch;
    _viewMax = maxPitch;
    _pitchNames = GeneratePitchNames(minPitch, maxPitch);
    _pitchValues = GeneratePitchValues(minPitch, maxPitch);
  }

  public static void SetOctaveRange(int minOctave, int maxOctave)
  {
    int minPitch = Math.Clamp(minOctave * 12, PicoMin, PicoMax - 12);
    int maxPitch = Math.Clamp(maxOctave * 12 + 11, minPitch + 11, PicoMax);
    SetViewRange(minPitch, maxPitch);
  }

  public static int MinOctaveFromView() => _viewMin / 12;
  public static int MaxOctaveFromView() => _viewMax / 12;

  public static string NoteToPitchName(int picoPitch)
  {
    if (picoPitch <= 0) return "--";
    int displayMidi = picoPitch + 12;
    string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    int octave = displayMidi / 12 - 1;
    int n = displayMidi % 12;
    return $"{notes[n]}{octave}";
  }

  public static int RowToMidi(int row) => RowToPitch(row);

  public static int RowToPitch(int row)
  {
    if (row < 0 || row >= _pitchValues.Count) return 48;
    return _pitchValues[row];
  }

  public static int MidiToRow(int pitch) => PitchToRow(pitch);

  public static int PitchToRow(int pitch)
  {
    if (pitch >= _viewMin && pitch <= _viewMax)
      return pitch - _viewMin;

    int best = 0;
    int bestDist = int.MaxValue;
    for (int i = 0; i < _pitchValues.Count; i++)
    {
      int dist = Math.Abs(_pitchValues[i] - pitch);
      if (dist < bestDist)
      {
        bestDist = dist;
        best = i;
      }
    }

    return best;
  }

  private static List<string> GeneratePitchNames(int minPitch, int maxPitch)
  {
    var names = new List<string>(maxPitch - minPitch + 1);
    for (int pitch = minPitch; pitch <= maxPitch; pitch++)
      names.Add(NoteToPitchName(pitch));
    return names;
  }

  private static List<int> GeneratePitchValues(int minPitch, int maxPitch)
  {
    var values = new List<int>(maxPitch - minPitch + 1);
    for (int pitch = minPitch; pitch <= maxPitch; pitch++)
      values.Add(pitch);
    return values;
  }
}
