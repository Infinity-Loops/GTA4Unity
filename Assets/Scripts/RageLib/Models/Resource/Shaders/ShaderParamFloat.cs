
using System.IO;

namespace RageLib.Models.Resource.Shaders
{
    public class ShaderParamFloat : IShaderParam
    {

        public float Data { get; private set; }

        #region Overrides of MaterialInfoDataObject
        public void Read(BinaryReader br)
        {
            Data = br.ReadSingle();
        }

        public void Write(BinaryWriter bw)
        {

        }
        #endregion
    }

}
