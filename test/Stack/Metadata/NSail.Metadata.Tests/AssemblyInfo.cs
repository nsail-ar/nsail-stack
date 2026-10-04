// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

// The test assembly declares its own shape to exercise the attribute path: under the
// default template Feature would bind "Tests"; under this one SubFeature does.
[assembly: NSail.Metadata.MetadataTemplate("{Root}.{Area}.{SubFeature}.*.{Object}")]
