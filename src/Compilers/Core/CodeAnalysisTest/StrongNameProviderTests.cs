﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class StrongNameProviderTests
    {
        [Fact]
        public void ResolveStrongNameKeyFile()
        {
            string fileName = "f.snk";
            string dir = @"C:\dir";
            string subdir = @"C:\dir\subdir";
            string filePath = dir + @"\" + fileName;
            string subFilePath = subdir + @"\" + fileName;

            var fs = new HashSet<string>
            {
                filePath,
                subFilePath
            };

            // with no search paths
            var provider = new VirtualizedStrongNameProvider(
                existingFullPaths: fs,
                searchPaths: ImmutableArray.Create(subdir));
            var subdirSearchPath = ImmutableArray.Create(subdir);

            // using base directory; base path ignored
            var path = provider.IOOp.ResolveStrongNameKeyFile(fileName, subdirSearchPath);
            Assert.Equal(subFilePath, path, StringComparer.OrdinalIgnoreCase);

            // search paths
            var searchPathsSP = ImmutableArray.Create(@"C:\goo", dir, subdir);

            path = provider.IOOp.ResolveStrongNameKeyFile(fileName, searchPathsSP);
            Assert.Equal(filePath, path, StringComparer.OrdinalIgnoreCase);

            // null base dir, no search paths
            var searchPathsEmpty = ImmutableArray.Create<string>();

            // relative path
            path = provider.IOOp.ResolveStrongNameKeyFile(fileName, searchPathsEmpty);
            Assert.Null(path);

            // full path
            path = provider.IOOp.ResolveStrongNameKeyFile(filePath, searchPathsEmpty);
            Assert.Equal(filePath, path, StringComparer.OrdinalIgnoreCase);

            // null base dir
            var searchPathsNullBaseSP = ImmutableArray.Create(dir, subdir);

            // relative path
            path = provider.IOOp.ResolveStrongNameKeyFile(fileName, searchPathsNullBaseSP);
            Assert.Equal(filePath, path, StringComparer.OrdinalIgnoreCase);

            // full path
            path = provider.IOOp.ResolveStrongNameKeyFile(filePath, searchPathsNullBaseSP);
            Assert.Equal(filePath, path, StringComparer.OrdinalIgnoreCase);
        }

        public class VirtualizedStrongNameProvider : DesktopStrongNameProvider
        {
            private class VirtualIOOperations : IOOperations
            {
                private HashSet<string> _existingFullPaths;
                public VirtualIOOperations(HashSet<string> existingFullPaths)
                {
                    _existingFullPaths = existingFullPaths;
                }

                internal override bool FileExists(string fullPath)
                {
                    return fullPath != null && _existingFullPaths != null && _existingFullPaths.Contains(FileUtilities.NormalizeAbsolutePath(fullPath));
                }
            }

            public VirtualizedStrongNameProvider(
                IEnumerable<string> existingFullPaths = null,
                ImmutableArray<string> searchPaths = default(ImmutableArray<string>))
                : base(searchPaths.NullToEmpty(), null, new VirtualIOOperations(new HashSet<string>(existingFullPaths, StringComparer.OrdinalIgnoreCase)))
            {
            }
        }
    }
}
