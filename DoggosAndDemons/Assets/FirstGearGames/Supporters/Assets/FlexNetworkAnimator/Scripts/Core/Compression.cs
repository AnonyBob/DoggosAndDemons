using FirstGearGames.Utilities.Networks;
using Mirror;

#pragma warning disable CS0618, CS0672
namespace FirstGearGames.Mirrors.Assets.FlexNetworkAnimators
{

    public static class Compressions
    {
        /// <summary>
        /// Compression levels for data.
        /// </summary>
        public enum CompressionLevels : byte
        {
            //No compression.
            None = 0,
            //Data can fit into a byte.
            Level1Positive = 1,
            Level1Negative = 2,
            //Data can fit into a short.
            Level2Positive = 3,
            Level2Negative = 4
        }

        #region WriteCompressed.
        /// <summary>
        /// Writes a compressed uint.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WriteCompressedUInt(NetworkWriter writer, uint value)
        {
            //Fits in a byte.
            if (value <= byte.MaxValue)
            {
                writer.WriteByte((byte)CompressionLevels.Level1Positive);
                writer.WriteByte((byte)value);
            }
            //Fits in a ushort
            else if (value <= ushort.MaxValue)
            {
                writer.WriteByte((byte)CompressionLevels.Level2Positive);
                writer.WriteUInt16((ushort)value);
            }
            //Cannot compress.
            else
            {
                writer.WriteByte((byte)CompressionLevels.None);
                writer.WriteUInt32(value);
            }
        }
        /// <summary>
        /// Writes a compressed int.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WriteCompressedInt(NetworkWriter writer, int value)
        {
            int absolute = (value >= 0) ? value : value * -1;
            bool positive = (value >= 0);
            //Fits in a byte.
            if (absolute <= byte.MaxValue)
            {
                if (positive)
                    writer.WriteByte((byte)CompressionLevels.Level1Positive);
                else
                    writer.WriteByte((byte)CompressionLevels.Level1Negative);

                writer.WriteByte((byte)absolute);
            }
            //Fits in a ushort
            else if (absolute <= ushort.MaxValue) 
            {
                if (positive)
                    writer.WriteByte((byte)CompressionLevels.Level2Positive);
                else
                    writer.WriteByte((byte)CompressionLevels.Level2Negative);

                writer.WriteUInt16((ushort)absolute);
            }
            //Cannot compress.
            else
            {
                writer.WriteByte((byte)CompressionLevels.None);
                writer.WriteInt32(value);
            }
        }
        /// <summary>
        /// Writes a compressed float.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WriteCompressedFloat(NetworkWriter writer, float value)
        {
            float multiplier = 100f;
            float maxByteValue = (byte.MaxValue - 1) / multiplier;
            float maxUShortValue = (ushort.MaxValue - 1) / multiplier;

            bool positive = (value >= 0);
            float absolute = (value >= 0f) ? value : value * -1f;
            //Fits in a byte.
            if (absolute <= maxByteValue)
            {
                if (positive)
                    writer.WriteByte((byte)CompressionLevels.Level1Positive);
                else
                    writer.WriteByte((byte)CompressionLevels.Level1Negative);

                writer.WriteByte((byte)(absolute * multiplier));
            }
            //Fits in a ushort
            else if (absolute < maxUShortValue)
            {
                if (positive)
                    writer.WriteByte((byte)CompressionLevels.Level2Positive);
                else
                    writer.WriteByte((byte)CompressionLevels.Level2Negative);

                writer.WriteUInt16((ushort)(absolute * multiplier));
            }
            //Cannot compress.
            else
            {
                writer.WriteByte((byte)CompressionLevels.None);
                writer.WriteSingle(value);
            }
        }
        #endregion

        #region ReadCompressed.
        /// <summary>
        /// Reads a compressed UInt.
        /// </summary>
        public static uint ReadCompressedUInt(NetworkReader reader)
        {
            CompressionLevels cl = (CompressionLevels)reader.ReadByte();

            //Compressed into byte.
            if (cl == CompressionLevels.Level1Positive)
                return reader.ReadByte();
            //Compressed into ushort.
            else if (cl == CompressionLevels.Level2Positive)
                return reader.ReadUInt16();
            //Not compressed.
            else
                return reader.ReadUInt32();
        }

        /// <summary>
        /// Reads a compressed int.
        /// </summary>
        public static int ReadCompressedInt(NetworkReader reader)
        {
            CompressionLevels cl = (CompressionLevels)reader.ReadByte();

            //Compressed into positive byte.
            if (cl == CompressionLevels.Level1Positive)
                return reader.ReadByte();
            //Compressed into negative byte.
            else if (cl == CompressionLevels.Level1Negative)
                return -reader.ReadByte();
            //Compressed into positive short.
            if (cl == CompressionLevels.Level2Positive)
                return reader.ReadUInt16();
            //Compressed into negative short.
            else if (cl == CompressionLevels.Level2Negative)
                return -reader.ReadUInt16();
            //Not compressed.
            else
                return reader.ReadInt32();
        }

        /// <summary>
        /// Reads a compressed float.
        /// </summary>
        public static float ReadCompressedFloat(NetworkReader reader)
        {
            CompressionLevels cl = (CompressionLevels)reader.ReadByte();
            float divisor = 100f;

            //Compressed into positive byte.
            if (cl == CompressionLevels.Level1Positive)
                return reader.ReadByte() / divisor;
            //Compressed into negative byte.
            else if (cl == CompressionLevels.Level1Negative)
                return -reader.ReadByte() / divisor;
            //Compressed into positive short.
            if (cl == CompressionLevels.Level2Positive)
                return reader.ReadUInt16() / divisor;
            //Compressed into negative short.
            else if (cl == CompressionLevels.Level2Negative)
                return -reader.ReadUInt16() / divisor;
            //Not compressed.
            else
                return reader.ReadSingle();
        } 
        #endregion


    }

}