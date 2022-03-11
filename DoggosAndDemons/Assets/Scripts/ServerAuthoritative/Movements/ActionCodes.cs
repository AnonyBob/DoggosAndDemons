using System;

namespace ServerAuthoritative.Movements
{
    [Flags]
    public enum ActionCodes : byte
    {
        None = 0,
        Bark = 1 << 0,
        Sprint = 1 << 1,
    }
}