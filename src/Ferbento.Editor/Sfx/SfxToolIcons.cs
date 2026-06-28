using Avalonia.Controls;
using Avalonia.Media;

namespace Ferbento.Editor.Sfx;

public static class SfxToolIcons
{
  public static Control CreatePencilIcon() => new PathIcon
  {
    Width = 16,
    Height = 16,
    Foreground = Brushes.White,
    Data = Geometry.Parse("M3,14 L5,12 L11,6 L10,5 L4,11 L2,13 Z M11,5 L12,4 L13,5 L12,6 Z"),
  };

  public static Control CreateEraserIcon() => new PathIcon
  {
    Width = 16,
    Height = 16,
    Foreground = Brushes.White,
    Data = Geometry.Parse("M4,12 L10,6 L13,9 L7,15 L4,12 Z M11,5 L12,4 L14,6 L13,7 Z"),
  };

  public static Control CreateSelectIcon() => new PathIcon
  {
    Width = 16,
    Height = 16,
    Foreground = Brushes.White,
    Data = Geometry.Parse("M3,3 L13,3 L13,13 L3,13 Z M5,5 L11,5 L11,11 L5,11 Z"),
  };

  public static Control CreateFillIcon() => new PathIcon
  {
    Width = 16,
    Height = 16,
    Foreground = Brushes.White,
    Data = Geometry.Parse("M4,12 L8,8 L12,12 L8,16 Z M10,6 L12,4 L14,6 L12,8 Z"),
  };

  public static Control CreatePlayIcon() => new PathIcon
  {
    Width = 14,
    Height = 14,
    Foreground = Brushes.White,
    Data = Geometry.Parse("M4,3 L12,8 L4,13 Z"),
  };

  public static Control CreateStopIcon() => new PathIcon
  {
    Width = 14,
    Height = 14,
    Foreground = Brushes.White,
    Data = Geometry.Parse("M4,4 L10,4 L10,10 L4,10 Z"),
  };
}
