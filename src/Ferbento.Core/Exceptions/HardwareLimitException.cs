namespace Ferbento.Core.Exceptions;

public sealed class HardwareLimitException : Exception
{
    public HardwareLimitException(string message) : base(message) { }
}
