// Vendored from https://github.com/S-Elephant/Elephant.NuGets/blob/master/Elephant.Uuidv5/Uuidv5Utils.cs
// Original: Copyright (c) 2022 SquirtingElephant, MIT License.
// Full license: Editor/ThirdParty/Elephant.Uuidv5-LICENSE.txt
// No functional modifications; only this header added.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Elephant.Uuidv5Utilities
{
    /// <summary>
    /// UUID v5 helper functions.
    /// </summary>
    public static class Uuidv5Utils
    {
        /// <summary>
        /// Generates a version 5 UUID based on a namespace ID and a name.
        /// </summary>
        public static Guid GenerateGuid(Guid namespaceId, string name)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] namespaceBytes = namespaceId.ToByteArray();
            SwapByteOrder(namespaceBytes);

            byte[] hash;
            using (SHA1 algorithm = SHA1.Create())
            {
                algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
                hash = algorithm.Hash;
            }

            byte[] result = new byte[16];
            Array.Copy(hash, 0, result, 0, 16);

            // Version 5 (high nibble of byte 6 = 5).
            result[6] = (byte)((result[6] & 0x0F) | (5 << 4));
            // RFC 4122 variant (top two bits of byte 8 = 10).
            result[8] = (byte)((result[8] & 0x3F) | 0x80);

            SwapByteOrder(result);
            return new Guid(result);
        }

        private static void SwapByteOrder(byte[] guidBytes)
        {
            Array.Reverse(guidBytes, 0, 4);
            Array.Reverse(guidBytes, 4, 2);
            Array.Reverse(guidBytes, 6, 2);
        }
    }
}
