using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor;

public interface IProfileAwareEditor
{
    void SetProfile(HardwareProfile profile);
    void SetChipBus(ChipBus bus);
    void RefreshFromBus() { }
    void RefreshPalette() { }
}
