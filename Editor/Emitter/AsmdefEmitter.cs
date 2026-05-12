namespace Narazaka.Unity.LilToonShaderMerger
{
    public static class AsmdefEmitter
    {
        public static string Emit(string name, string lilToonEditorGuid)
        {
            return @"{
  ""name"": """ + name + @""",
  ""includePlatforms"": [""Editor""],
  ""references"": [""GUID:" + lilToonEditorGuid + @"""],
  ""autoReferenced"": false
}
";
        }
    }
}
