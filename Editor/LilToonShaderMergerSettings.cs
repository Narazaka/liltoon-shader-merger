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
        [Tooltip("#include で参照されているファイルは常にコピーされる。オンにすると、正規ファイルと .lilcontainer 以外のソースフォルダ内の全ファイルもコピーする " +
                 "(マクロ経由の include 等、参照追跡で拾えない場合向け)。")]
        public bool copyAllExtraFiles = false;
        public InspectorStrategy inspectorStrategy = InspectorStrategy.MergeOrSkip;
    }
}
