using System;
using System.Text;
using Elephant.Uuidv5Utilities;

namespace Narazaka.Unity.LilToonShaderMerger
{
    /// <summary>
    /// Deterministic .meta GUID derivation and emission for merged shader output.
    /// See docs/superpowers/specs/2026-05-20-deterministic-merged-shader-guids-design.md.
    /// </summary>
    public static class MetaGuidEmitter
    {
        // Tool-specific namespace UUID. Generated once; never change — changing this
        // would shift every merged shader's GUID for every user, breaking material refs.
        static readonly Guid NamespaceGuid = new Guid("c4f1a2e8-7d3b-4e5c-9a06-1f8e2d4b6a09");

        /// <summary>
        /// 32-char lowercase hex GUID derived from (shaderName, relativePath).
        /// </summary>
        public static string DeterministicGuid(string shaderName, string relativePath)
        {
            if (shaderName == null) throw new ArgumentNullException(nameof(shaderName));
            if (relativePath == null) throw new ArgumentNullException(nameof(relativePath));
            var name = shaderName + "\n" + relativePath.Replace('\\', '/');
            var g = Uuidv5Utils.GenerateGuid(NamespaceGuid, name);
            return g.ToString("N"); // 32 lowercase hex, no dashes; deterministic
        }

        /// <summary>
        /// Compute the path of <paramref name="full"/> relative to <paramref name="root"/>
        /// using forward slashes. Throws if <paramref name="full"/> is not under <paramref name="root"/>.
        /// </summary>
        public static string Relative(string root, string full)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (full == null) throw new ArgumentNullException(nameof(full));
            var r = root.Replace('\\', '/').TrimEnd('/');
            var f = full.Replace('\\', '/');
            if (f == r) return "";
            var prefix = r + "/";
            if (!f.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException($"path '{full}' is not under root '{root}'");
            return f.Substring(prefix.Length);
        }
    }
}
