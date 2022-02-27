using System;

namespace FirstGearGames.Mirrors.Assets.FlexNetworkAnimators
{


    [System.Serializable]
    public struct AnimatorUpdate
    {
        public byte ComponentIndex;
        public uint NetworkIdentity;
        public ArraySegment<byte> Data;
        public AnimatorUpdate(byte componentIndex, uint networkIdentity, ArraySegment<byte> data)
        {
            ComponentIndex = componentIndex;
            NetworkIdentity = networkIdentity;
            Data = data;
        }
    }



}