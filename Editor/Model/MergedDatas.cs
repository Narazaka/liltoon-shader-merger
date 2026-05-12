using System.Collections.Generic;

namespace Narazaka.Unity.LilToonShaderMerger
{
    public class MergedDatas
    {
        public string ShaderName { get; set; }
        public string EditorName { get; set; }
        public List<ReplaceDirective> Replaces { get; } = new List<ReplaceDirective>();
        // key (例: "InsertPassPre") → 各ソースが指定した file path 群
        public Dictionary<string, List<string>> InsertFilePaths { get; } = new Dictionary<string, List<string>>();
    }
}
