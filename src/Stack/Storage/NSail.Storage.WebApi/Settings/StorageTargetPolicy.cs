// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Problems;
using NSail.Security;
using NSail.Storage.Providers;

namespace NSail.Storage.Settings;

// Where a saved or tested target may point. Both the save and the test go through it: the test
// reaches whatever the row says, whoever wrote the row.
[Injectable]
public sealed class StorageTargetPolicy
{
    readonly StorageGuardOptions _options;

    public StorageTargetPolicy(StorageGuardOptions options)
    {
        _options = options;
    }

    public async Task Check(string? provider, string? container, string? endpoint, CancellationToken cancellationToken = default)
    {
        if (string.Equals(provider?.Trim(), "file", StringComparison.OrdinalIgnoreCase)
            && container?.Trim() is { Length: > 0 } directory
            && !UnderRoot(directory))
        {
            throw Refusal("StorageContainerOutsideRoot", nameof(StorageTarget.Container), "The directory is not under a root this install allows.");
        }

        if (endpoint?.Trim() is { Length: > 0 } address)
        {
            await CheckEndpoint(address, cancellationToken);
        }
    }

    bool UnderRoot(string directory)
    {
        string full;

        try
        {
            full = Path.GetFullPath(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            return false;
        }

        foreach (var root in _options.FileRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var rooted = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);

            if (full.TrimEnd(Path.DirectorySeparatorChar).Equals(rooted, StringComparison.Ordinal)
                || full.StartsWith(rooted + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    async Task CheckEndpoint(string address, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || !_options.AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)
            || uri.UserInfo.Length > 0
            || uri.Host.Length == 0)
        {
            throw Refusal("StorageEndpointNotAllowed", nameof(StorageTarget.Endpoint), "The endpoint's scheme or host is not allowed by this install.");
        }

        var host = uri.Host.Trim('[', ']').TrimEnd('.');

        if (_options.AllowedHosts.Length > 0
            && !_options.AllowedHosts.Any(allowed => host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase)))
        {
            throw Refusal("StorageEndpointNotAllowed", nameof(StorageTarget.Endpoint), "The endpoint's scheme or host is not allowed by this install.");
        }

        if (_options.AllowPrivateAddresses)
        {
            return;
        }

        if (await PrivateHost.Is(host, cancellationToken))
        {
            throw Refusal("StorageEndpointNotAllowed", nameof(StorageTarget.Endpoint), "The endpoint's scheme or host is not allowed by this install.");
        }
    }

    static BusinessException Refusal(string rule, string field, string message)
    {
        return new BusinessException(BusinessProblem.RuleViolation(rule, field, message));
    }
}
