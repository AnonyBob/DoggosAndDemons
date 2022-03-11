namespace ServerAuthoritative.Movements
{
    public struct MovementClientInput
    {
        public uint TickNumber;

        public float Vertical;
        public float Horizontal;

        public byte Actions;
    }
}