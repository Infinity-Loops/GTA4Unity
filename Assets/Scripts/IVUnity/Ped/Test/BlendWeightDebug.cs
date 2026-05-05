using UnityEngine;

namespace IVUnity.Ped
{
    public class BlendWeightDebug : MonoBehaviour
    {
        public int HighlightBoneIndex = -1;

        private static readonly Color[] BoneColors = GenerateBoneColors(80);
        private Material _debugMat;
        private SkinnedMeshRenderer[] _smrs;

        static Color[] GenerateBoneColors(int count)
        {
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                float hue = (i * 0.618034f) % 1f;
                colors[i] = Color.HSVToRGB(hue, 0.85f, 0.95f);
            }
            return colors;
        }

        void Start()
        {
            _debugMat = new Material(Shader.Find("Debug/BlendWeights"));
            _smrs = GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var smr in _smrs)
                BakeBlendColors(smr);

            LogBoneColorLegend();
        }

        void BakeBlendColors(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null) return;

            var boneWeights = mesh.boneWeights;
            if (boneWeights == null || boneWeights.Length == 0) return;

            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < boneWeights.Length; i++)
            {
                var bw = boneWeights[i];
                Color c = Color.black;
                if (bw.boneIndex0 < BoneColors.Length)
                    c += BoneColors[bw.boneIndex0] * bw.weight0;
                if (bw.boneIndex1 < BoneColors.Length)
                    c += BoneColors[bw.boneIndex1] * bw.weight1;
                if (bw.boneIndex2 < BoneColors.Length)
                    c += BoneColors[bw.boneIndex2] * bw.weight2;
                if (bw.boneIndex3 < BoneColors.Length)
                    c += BoneColors[bw.boneIndex3] * bw.weight3;
                colors[i] = c;
            }

            mesh.colors = colors;
            smr.sharedMaterial = _debugMat;
        }

        void Update()
        {
            if (HighlightBoneIndex < 0) return;

            foreach (var smr in _smrs)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                var boneWeights = mesh.boneWeights;
                if (boneWeights == null) continue;

                var colors = new Color[mesh.vertexCount];
                for (int i = 0; i < boneWeights.Length; i++)
                {
                    var bw = boneWeights[i];
                    float w = 0;
                    if (bw.boneIndex0 == HighlightBoneIndex) w += bw.weight0;
                    if (bw.boneIndex1 == HighlightBoneIndex) w += bw.weight1;
                    if (bw.boneIndex2 == HighlightBoneIndex) w += bw.weight2;
                    if (bw.boneIndex3 == HighlightBoneIndex) w += bw.weight3;
                    colors[i] = Color.Lerp(Color.black, Color.red, w);
                }
                mesh.colors = colors;
            }
        }

        void LogBoneColorLegend()
        {
            if (_smrs == null || _smrs.Length == 0) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[BlendDebug] === BONE INDEX → BODY AREA MAP ===");

            foreach (var smr in _smrs)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                var boneWeights = mesh.boneWeights;
                var verts = mesh.vertices;
                if (boneWeights == null || verts == null) continue;

                var bones = smr.bones;
                int maxBone = 0;
                foreach (var bw in boneWeights)
                {
                    if (bw.boneIndex0 > maxBone) maxBone = bw.boneIndex0;
                    if (bw.weight1 > 0.01f && bw.boneIndex1 > maxBone) maxBone = bw.boneIndex1;
                }

                var sumPos = new Vector3[maxBone + 1];
                var sumWeight = new float[maxBone + 1];
                var count = new int[maxBone + 1];

                for (int i = 0; i < boneWeights.Length; i++)
                {
                    var bw = boneWeights[i];
                    var p = verts[i];
                    if (bw.weight0 > 0.01f && bw.boneIndex0 <= maxBone)
                    {
                        sumPos[bw.boneIndex0] += p * bw.weight0;
                        sumWeight[bw.boneIndex0] += bw.weight0;
                        count[bw.boneIndex0]++;
                    }
                    if (bw.weight1 > 0.01f && bw.boneIndex1 <= maxBone)
                    {
                        sumPos[bw.boneIndex1] += p * bw.weight1;
                        sumWeight[bw.boneIndex1] += bw.weight1;
                        count[bw.boneIndex1]++;
                    }
                }

                sb.AppendLine($"  Mesh '{smr.name}' ({boneWeights.Length} verts, maxBoneIdx={maxBone}):");
                for (int i = 0; i <= maxBone; i++)
                {
                    if (count[i] == 0) continue;
                    var avg = sumPos[i] / sumWeight[i];
                    string boneName = (bones != null && i < bones.Length && bones[i] != null)
                        ? bones[i].name : $"?{i}";
                    sb.AppendLine($"    boneIdx[{i,2}] = {boneName,-40} verts={count[i],5} avgPos=({avg.x:+0.000;-0.000},{avg.y:+0.000;-0.000},{avg.z:+0.000;-0.000})");
                }
            }

            Debug.Log(sb.ToString());
        }
    }
}
