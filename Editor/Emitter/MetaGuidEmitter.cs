using System;
using System.Text;
using Narazaka.Unity.LilToonShaderMerger.ThirdParty.Uuidv5;

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

        /// <summary>
        /// Build the full text of a Unity .meta file for <paramref name="assetPathOrName"/>
        /// (only the extension / "is folder" matters; full path not required).
        /// </summary>
        /// <param name="sourceImporterBlock">
        /// For .lilcontainer (and unknown extensions where we have a sibling source .meta),
        /// pass the importer block from the source .meta verbatim (everything after the
        /// 'guid:' line). Pass null to use the per-extension template.
        /// </param>
        public static string BuildMetaText(
            string assetPathOrName,
            string guid,
            string sourceImporterBlock,
            bool isFolder = false)
        {
            if (guid == null || guid.Length != 32)
                throw new ArgumentException("guid must be 32 hex chars", nameof(guid));

            var sb = new StringBuilder();
            sb.Append("fileFormatVersion: 2\n");
            sb.Append("guid: ").Append(guid).Append('\n');
            if (isFolder) sb.Append("folderAsset: yes\n");

            string importer;
            if (isFolder)
            {
                importer = DefaultImporterBlock();
            }
            else
            {
                var ext = ExtensionOf(assetPathOrName);
                if (ext == ".lilcontainer" && sourceImporterBlock != null)
                {
                    importer = sourceImporterBlock.TrimEnd('\n') + "\n";
                }
                else if (sourceImporterBlock != null && IsUnknownExtension(ext))
                {
                    // Reuse the source's importer block (e.g. extra files copied from a source folder).
                    importer = sourceImporterBlock.TrimEnd('\n') + "\n";
                }
                else
                {
                    importer = ImporterBlockFor(ext);
                }
            }
            sb.Append(importer);
            return sb.ToString();
        }

        /// <summary>Atomically write the .meta file for <paramref name="assetPath"/>. Overwrites any existing meta.</summary>
        public static void WriteMeta(
            string assetPath,
            string guid,
            string sourceImporterBlock,
            bool isFolder = false)
        {
            var meta = BuildMetaText(assetPath, guid, sourceImporterBlock, isFolder);
            System.IO.File.WriteAllText(assetPath + ".meta", meta);
        }

        static string ExtensionOf(string path)
        {
            var i = path.LastIndexOf('.');
            if (i < 0) return "";
            return path.Substring(i).ToLowerInvariant();
        }

        static bool IsUnknownExtension(string ext)
        {
            switch (ext)
            {
                case ".hlsl":
                case ".lilblock":
                case ".lilcontainer":
                case ".cs":
                case ".asmdef":
                    return false;
                default:
                    return true;
            }
        }

        static string ImporterBlockFor(string ext)
        {
            switch (ext)
            {
                case ".hlsl":          return ShaderIncludeImporterBlock();
                case ".lilblock":      return DefaultImporterBlock();
                case ".cs":            return MonoImporterBlock();
                case ".asmdef":        return AssemblyDefinitionImporterBlock();
                case ".lilcontainer":  return DefaultImporterBlock(); // last-resort; should pass sourceImporterBlock
                default:               return DefaultImporterBlock();
            }
        }

        static string DefaultImporterBlock() =>
            "DefaultImporter:\n" +
            "  externalObjects: {}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";

        static string ShaderIncludeImporterBlock() =>
            "ShaderIncludeImporter:\n" +
            "  externalObjects: {}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";

        static string MonoImporterBlock() =>
            "MonoImporter:\n" +
            "  externalObjects: {}\n" +
            "  serializedVersion: 2\n" +
            "  defaultReferences: []\n" +
            "  executionOrder: 0\n" +
            "  icon: {instanceID: 0}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";

        static string AssemblyDefinitionImporterBlock() =>
            "AssemblyDefinitionImporter:\n" +
            "  externalObjects: {}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";
    }
}
