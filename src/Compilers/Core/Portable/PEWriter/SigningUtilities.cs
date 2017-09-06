﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.Cci
{
    internal static class SigningUtilities
    {
        internal static byte[] CalculateRsaSignature(IEnumerable<Blob> content, RSAParameters privateKey)
        {
            var hash = CalculateSha1(content);

            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(privateKey);
                var signature = rsa.SignHash(hash, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                Array.Reverse(signature);
                return signature;
            }
        }

        private static byte[] CalculateSha1(IEnumerable<Blob> content)
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
            {
                var stream = new MemoryStream();

                foreach (var blob in content)
                {
                    var segment = blob.GetBytes();

                    stream.Write(segment.Array, segment.Offset, segment.Count);

                    hash.AppendData(segment.Array, segment.Offset, segment.Count);
                }

                return hash.GetHashAndReset();
            }
        }
        internal static int CalculateStrongNameSignatureSize(CommonPEModuleBuilder module, RSAParameters? privateKey)
        {
            ISourceAssemblySymbolInternal assembly = module.SourceAssemblyOpt;
            if (assembly == null && !privateKey.HasValue)
            {
                return 0;
            }

            int keySize = 0;

            // EDMAURER the count of characters divided by two because the each pair of characters will turn in to one byte.
            if (keySize == 0 && assembly != null)
            {
                keySize = (assembly.SignatureKey == null) ? 0 : assembly.SignatureKey.Length / 2;
            }

            if (keySize == 0 && assembly != null)
            {
                keySize = assembly.Identity.PublicKey.Length;
            }

            if (keySize == 0 && privateKey.HasValue)
            {
                keySize = privateKey.Value.Modulus.Length;
            }


            if (keySize == 0)
            {
                return 0;
            }

            return (keySize < 128 + 32) ? 128 : keySize - 32;
        }
    }
}
