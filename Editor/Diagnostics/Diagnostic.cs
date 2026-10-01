namespace Narazaka.Unity.LilToonShaderMerger
{
    public enum Severity { Warning, Error }
    public class Diagnostic
    {
        public Severity Severity { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public override string ToString() => $"[{Severity}][{Category}] {Message}";
    }

    // 合成後の custom.hlsl。出力順の要素列
    public class MergedHlsl
    {
        public System.Collections.Generic.List<HlslEntry> Entries { get; } = new System.Collections.Generic.List<HlslEntry>();

        public HlslEntry FindDefine(string name) =>
            Entries.FindLast(e => e.Kind == HlslEntryKind.Define && e.Name == name);
    }
}
