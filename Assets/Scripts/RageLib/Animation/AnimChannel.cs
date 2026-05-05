using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;

namespace RageLib.Animation
{
    public abstract class AnimChannel
    {
        public byte Type { get; protected set; }

        public abstract float[] GetValues(int numFrames);

        public static AnimChannel ReadChannel(BinaryReader br, ushort numFrames)
        {
            uint vftable = br.ReadUInt32();
            byte flags = br.ReadByte();
            byte type = br.ReadByte();
            br.ReadUInt16(); // padding

            switch (type)
            {
                case 4: // StaticFloat
                    return new StaticFloatChannel(br);
                case 6: // QuantizeFloat
                    return new QuantizeFloatChannel(br, numFrames);
                case 1: // RawFloat
                    return new RawFloatChannel(br, numFrames);
                default:
                    return new UnknownChannel(type, numFrames);
            }
        }
    }

    public class StaticFloatChannel : AnimChannel
    {
        public float Value { get; private set; }

        public StaticFloatChannel(BinaryReader br)
        {
            Type = 4;
            Value = br.ReadSingle();
        }

        public override float[] GetValues(int numFrames)
        {
            var values = new float[numFrames];
            for (int i = 0; i < numFrames; i++)
                values[i] = Value;
            return values;
        }
    }

    public class QuantizeFloatChannel : AnimChannel
    {
        public float Scale { get; private set; }
        public float Offset { get; private set; }
        public float[] DecodedValues { get; private set; }

        public QuantizeFloatChannel(BinaryReader br, ushort numFrames)
        {
            Type = 6;

            // atPackedArray: ptr, elementSize, elementMax
            long elementsPtr = ResourceUtil.ReadOffset(br);
            uint elementSize = br.ReadUInt32();
            uint elementMax = br.ReadUInt32();
            Scale = br.ReadSingle();
            Offset = br.ReadSingle();

            DecodedValues = new float[numFrames];

            if (elementsPtr <= 0 || elementSize == 0 || elementMax == 0)
            {
                for (int i = 0; i < numFrames; i++)
                    DecodedValues[i] = Offset;
                return;
            }

            // Read packed data
            uint totalBits = elementMax * elementSize;
            uint numU32s = (totalBits + 31) / 32;
            uint[] packedData;

            using (new StreamContext(br))
            {
                br.BaseStream.Seek(elementsPtr, SeekOrigin.Begin);
                packedData = new uint[numU32s + 1]; // +1 for safe boundary read
                for (int i = 0; i < numU32s + 1 && br.BaseStream.Position < br.BaseStream.Length; i++)
                    packedData[i] = br.ReadUInt32();
            }

            // Decode: replicate atPackedArray::GetElement
            ulong mask = (1UL << (int)elementSize) - 1;
            int count = (int)System.Math.Min(elementMax, (uint)numFrames);
            for (int i = 0; i < count; i++)
            {
                uint address = (uint)i * elementSize;
                uint block = address >> 5;
                int bit = (int)(address & 31);
                ulong word = packedData[block] | ((ulong)packedData[block + 1] << 32);
                uint quantized = (uint)((word >> bit) & mask);
                DecodedValues[i] = quantized * Scale + Offset;
            }

            // Fill remaining frames with last value
            float lastVal = count > 0 ? DecodedValues[count - 1] : Offset;
            for (int i = count; i < numFrames; i++)
                DecodedValues[i] = lastVal;
        }

        public override float[] GetValues(int numFrames)
        {
            return DecodedValues;
        }
    }

    public class RawFloatChannel : AnimChannel
    {
        public float[] Values { get; private set; }

        public RawFloatChannel(BinaryReader br, ushort numFrames)
        {
            Type = 1;

            long dataPtr = ResourceUtil.ReadOffset(br);
            ushort count = br.ReadUInt16();
            br.ReadUInt16(); // capacity

            Values = new float[numFrames];

            if (dataPtr > 0 && count > 0)
            {
                using (new StreamContext(br))
                {
                    br.BaseStream.Seek(dataPtr, SeekOrigin.Begin);
                    int readCount = System.Math.Min(count, numFrames);
                    for (int i = 0; i < readCount; i++)
                        Values[i] = br.ReadSingle();
                    float last = readCount > 0 ? Values[readCount - 1] : 0;
                    for (int i = readCount; i < numFrames; i++)
                        Values[i] = last;
                }
            }
        }

        public override float[] GetValues(int numFrames)
        {
            return Values;
        }
    }

    public class UnknownChannel : AnimChannel
    {
        private int _numFrames;

        public UnknownChannel(byte type, ushort numFrames)
        {
            Type = type;
            _numFrames = numFrames;
        }

        public override float[] GetValues(int numFrames)
        {
            return new float[numFrames];
        }
    }
}
