namespace Narazaka.Unity.LilToonShaderMerger
{
    public enum Severity { Warning, Error }
    public class Diagnostic
    {
        public Severity Severity { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public override string ToString() => $"[{Severity}] {Category}: {Message}";
    }

    public class MergedHlsl
    {
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> MultilineMacros { get; } = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
        public System.Collections.Generic.HashSet<string> FlagMacros { get; } = new System.Collections.Generic.HashSet<string>();
    }
}
