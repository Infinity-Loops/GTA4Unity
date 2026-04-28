using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace IVUnity.Editor
{
    public class ShaderBuildPreprocess : IPreprocessBuildWithReport
    {
        private const string ShaderFolder = "Assets/Shaders";

        private static readonly string[] RequiredShaders =
        {
            "Legacy Shaders/Diffuse",
            "Hidden/CubeBlur",
            "Hidden/CubeCopy",
            "Hidden/CubeBlend",
            "Sprites/Default",
            "UI/Default",
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_AlwaysIncludedShaders");

            prop.ClearArray();

            int index = 0;

            foreach (var name in RequiredShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;

                prop.arraySize = index + 1;
                prop.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                index++;
            }

            var guids = AssetDatabase.FindAssets("t:Shader", new[] { ShaderFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;

                prop.arraySize = index + 1;
                prop.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                index++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ShaderBuildPreprocess] Set {index} Always Included Shaders ({RequiredShaders.Length} built-in + {index - RequiredShaders.Length} from {ShaderFolder})");
        }
    }
}
