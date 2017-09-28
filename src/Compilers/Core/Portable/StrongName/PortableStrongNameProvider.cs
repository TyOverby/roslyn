﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Cci;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    internal sealed class PortableStrongNameProvider : StrongNameProvider
    {
        private readonly ImmutableArray<string> _keyFileSearchPaths;
        internal StrongNameFileSystem FileSystem { get; set; }

        public PortableStrongNameProvider(ImmutableArray<string> keySearchPaths, StrongNameFileSystem strongNameFileSystem)
        {
            FileSystem = strongNameFileSystem ?? StrongNameFileSystem.s_StrongNameFileSystemInstance;
            _keyFileSearchPaths = keySearchPaths.NullToEmpty();
        }

        public override int GetHashCode()
        {
            return 0;
        }

        internal override SigningCapability Capability => SigningCapability.SignsPeBuilder;

        internal override StrongNameKeys CreateKeys(string keyFilePath, string keyContainerName, CommonMessageProvider messageProvider)
        {
            var keyPair = default(ImmutableArray<byte>);
            var publicKey = default(ImmutableArray<byte>);
            string container = null;

            if (!string.IsNullOrEmpty(keyFilePath))
            {
                try
                {
                    string resolvedKeyFile = FileSystem.ResolveStrongNameKeyFile(keyFilePath, _keyFileSearchPaths);
                    if (resolvedKeyFile == null)
                    {
                        // Used for getting the exception message.
                        var exception = new FileNotFoundException(CodeAnalysisResources.FileNotFound, keyFilePath);
                        return new StrongNameKeys(StrongNameKeys.GetKeyFileError(messageProvider, keyFilePath, exception.Message));
                    }

                    Debug.Assert(PathUtilities.IsAbsolute(resolvedKeyFile));
                    var fileContent = ImmutableArray.Create(FileSystem.ReadAllBytes(resolvedKeyFile));
                    return StrongNameKeys.CreateHelper(fileContent, keyFilePath);
                }
                catch (Exception ex)
                {
                    return new StrongNameKeys(StrongNameKeys.GetKeyFileError(messageProvider, keyFilePath, ex.Message));
                }
                // it turns out that we don't need IClrStrongName to retrieve a key file,
                // so there's no need for a catch of ClrStrongNameMissingException in this case
            }

            return new StrongNameKeys(keyPair, publicKey, null, container, keyFilePath);
        }

        internal override void SignPeBuilder(ExtendedPEBuilder peBuilder, BlobBuilder peBlob, RSAParameters privkey)
        {
            peBuilder.Sign(peBlob, content => SigningUtilities.CalculateRsaSignature(content, privkey));
        }

        internal override Stream CreateInputStream()
        {
            throw new NotSupportedException();
        }

        public override bool Equals(object obj)
        {
            if (obj is null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (PortableStrongNameProvider)obj;
            return FileSystem.Equals(other.FileSystem) &&
                _keyFileSearchPaths.SequenceEqual(other._keyFileSearchPaths);
                    
        }
    }
}
