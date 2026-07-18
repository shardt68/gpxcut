namespace GpxCut.Core.IO;

public sealed class GpxReadException : Exception
{
    public GpxReadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
