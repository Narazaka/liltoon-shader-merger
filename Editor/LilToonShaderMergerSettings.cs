using UnityEditor;
using UnityEngine;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public enum ConflictStrategy { ErrorOut, PreferFirst, PreferLast }
    public enum InspectorStrategy { MergeOrSkip }

    [CreateAssetMenu(menuName = "lilToon Shader Merger/Merger Settings", fileName = "ShaderMergerSettings")]
    public class LilToonShaderMergerSettings : ScriptableObject
    {
        [Header("Input")]
        public DefaultAsset[] sourceFolders;

        [Header("Output")]
        public string shaderName = "";
        public DefaultAsset outputFolder;
        public string editorClassName = "";

        [Header("Conflict Strategy")]
        public ConflictStrategy propertyConflict = ConflictStrategy.ErrorOut;
        public ConflictStrategy functionConflict = ConflictStrategy.ErrorOut;
        public ConflictStrategy replaceConflict  = ConflictStrategy.ErrorOut;
        public ConflictStrategy textureConflict  = ConflictStrategy.ErrorOut;

        [Header("Options")]
        public bool dedupeIdenticalIncludes = true;
        [Tooltip("custom.hlsl / custom_insert.hlsl / *.lilblock 以外のファイル (追加の .hlsl 等) を出力フォルダへコピーする。" +
                 "元シェーダーの .lilcontainer がそれらを #include している場合、オフにすると生成シェーダーがコンパイルエラーになる。")]
        public bool copyExtraFiles = true;
        public InspectorStrategy inspectorStrategy = InspectorStrategy.MergeOrSkip;
    }
}
