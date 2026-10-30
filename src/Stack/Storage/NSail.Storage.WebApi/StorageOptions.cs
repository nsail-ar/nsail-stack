// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Storage.Providers;

namespace NSail.Storage;

// Where the install writes when a business has configured nothing of its own. Configuration and
// not a row: the directory a container mounts a volume at is something the deploy knows and a
// business never will, and a tenant that never opened the settings screen still uploads a logo.
// Everything empty means the install named nowhere, and a put is refused by name instead.
//
// The field names are the target's own, so an install that would rather its default were an
// S3-compatible bucket names one in the same section.
public class StorageOptions : IValidatableObject
{
    public string Provider { get; set; } = "";

    public string Endpoint { get; set; } = "";

    public string Region { get; set; } = "";

    // The container inside the target: a directory on the host's disk for the filesystem
    // provider, a bucket for an S3 one.
    public string Container { get; set; } = "";

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    // The ref a repository provider commits to; empty is the provider's own default.
    public string Branch { get; set; } = "";

    // The address pattern a repository's objects are read at, `{key}` standing for the object.
    public string LinkTemplate { get; set; } = "";

    public StorageTarget Target()
    {
        if (Provider.Trim() is not { Length: > 0 } provider)
        {
            return StorageTarget.None;
        }

        return new StorageTarget(
            provider,
            Endpoint.Trim(),
            Region.Trim(),
            Container.Trim(),
            AccessKey.Trim(),
            SecretKey.Trim(),
            Branch.Trim(),
            LinkTemplate.Trim());
    }

    // A destination half typed would have every tenant with no settings row of its own refused
    // at its first upload, and nothing would say why. It is refused at startup instead, where a
    // person is looking. Only this direction: the container is the one thing every provider
    // needs, while a deploy that names only the directory it mounted is naming the whole target
    // an app whose floor is its own disk has (AddFileSystemStorage's OnDisk).
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Provider.Trim().Length > 0 && Container.Trim().Length == 0)
        {
            yield return new ValidationResult(
                $"{nameof(Container)} is required once a default storage provider is named.",
                [nameof(Container)]);
        }
    }
}
