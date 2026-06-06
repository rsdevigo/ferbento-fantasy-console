namespace Ferbento.Core.Chips;

public interface IChip
{
    string Name { get; }
    void Initialize(ChipBus bus);
    void Reset();
    void Tick();
}
