
using System.IO;

namespace RageLib.Common.ResourceTypes
{
    public struct Matrix43 : IFileAccess
    {
        private float[] M;

        public static Matrix43 Identity
        {
            get
            {
                var m = new Matrix43
                {
                    M = new[]
                    {
                    1f, 0f, 0f,
                    0f, 1f, 0f,
                    0f, 0f, 1f,
                    0f, 0f, 0f,
                }
                };
                return m;
            }
        }

        public float this[int i, int j]
        {
            get { return M[i * 4 + j]; }
            set { M[i * 4 + j] = value; }
        }

        public float this[int m]
        {
            get { return M[m]; }
            set { M[m] = value; }
        }

        public Matrix43(BinaryReader br)
            : this()
        {
            Read(br);
        }

        public void Read(BinaryReader br)
        {
            M = new float[12];
            for (int i = 0; i < 12; i++)
            {
                M[i] = br.ReadSingle();
            }
        }

        public void Write(BinaryWriter bw)
        {
            for (int i = 0; i < 12; i++)
            {
                bw.Write(M[i]);
            }
        }

    }
}