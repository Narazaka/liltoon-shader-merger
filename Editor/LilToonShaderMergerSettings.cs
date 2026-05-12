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
        public bool copyExtraFiles = false;
        public InspectorStrategy inspectorStrategy = InspectorStrategy.MergeOrSkip;
    }
}
