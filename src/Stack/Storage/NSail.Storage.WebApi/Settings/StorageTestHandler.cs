// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Problems;
using NSail.Storage.Objects;
using System.Text;

namespace NSail.Storage.Settings;

// The whole round trip against the saved target, before anything depends on it: write a probe,
// read it back, prove the bytes survived, list it, remove it. A test that only wrote would pass
// against a bucket the install cannot read from, and one that left its probe behind would
// litter the container it just proved.
//
// A probe that fails is a REFUSAL and answers like one — nothing is caught into a Succeeded flag.
// Two kinds of refusal travel as they came, because the install words them itself: one anchored
// on a field (StorageManager names Proveedor when the install stores nowhere, a provider names
// Endpoint when it has none), which lands on the field this screen renders instead of in a toast,
// and the probe's own verdicts, which name no field and fall to the foot of the form, each under a
// rule of its own. Anything else — a provider's refusal that carries the far end's own text, an S3
// body or git's stderr — answers the fixed StorageTestFailed, and the server log keeps the text.
[Injectable]
public class StorageTestHandler : IHandler<TestStorageTarget>
{
    const string NotReadBack = "StorageTestNotReadBack";
    const string ContentDiffers = "StorageTestContentDiffers";
    const string NotListed = "StorageTestNotListed";

    readonly StorageStores _stores;
    readonly IServiceProvider _services;
    readonly StorageCredentialProvider _credentials;
    readonly StorageTargetPolicy _policy;
    readonly ILogger<StorageTestHandler> _logger;

    public StorageTestHandler(
        StorageStores stores,
        IServiceProvider services,
        StorageCredentialProvider credentials,
        StorageTargetPolicy policy,
        ILogger<StorageTestHandler> logger)
    {
        _credentials = credentials;
        _policy = policy;
        _logger = logger;
        _stores = stores;
        _services = services;
    }

    public async Task Handle(TestStorageTarget message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The name's own manager, so the probe lands where that name's consumer writes — its own
        // target, or the default's where it has none. The name arrives at runtime, so the keyed
        // manager is asked for here rather than injected.
        var name = _stores.Require(message.Name);

        if (await _credentials.Row(name, cancellationToken) is { } row)
        {
            await _policy.Check(row.Provider, row.Container, row.Endpoint, cancellationToken);
        }

        var storage = _services.GetRequiredKeyedService<StorageManager>(name);
        var key = storage.Key("nsail-storage-test", $"{Guid.NewGuid():N}.txt");
        var payload = Encoding.UTF8.GetBytes($"NSail storage test {DateTimeOffset.UtcNow:O}");

        try
        {
            using (var content = new MemoryStream(payload))
            {
                await storage.Put(key, content, "text/plain", cancellationToken);
            }

            using var stored = await storage.Get(key, cancellationToken);

            if (stored is null)
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    NotReadBack,
                    "The object was written but could not be read back."));
            }

            using var readBack = new MemoryStream();
            await stored.Content.CopyToAsync(readBack, cancellationToken);

            if (!readBack.ToArray().AsSpan().SequenceEqual(payload))
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    ContentDiffers,
                    "The object read back does not match what was written."));
            }

            var listed = await storage.List("nsail-storage-test/", cancellationToken);

            if (!listed.Any(entry => entry.Key == key))
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    NotListed,
                    "The object was written but does not appear when the container is listed."));
            }

            await storage.Delete(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Swept whatever the verdict was, so a target that half-worked is not left holding the
            // probe.
            await Sweep(storage, key, cancellationToken);

            if (exception is BusinessException refusal && IsWordedHere(refusal))
            {
                throw;
            }

            // A BusinessException's own message is its title; the far end's text is in its issues.
            var reason = exception is BusinessException business
                ? string.Join(" ", business.Issues.Select(issue => issue.Message))
                : exception.Message;

            _logger.LogWarning(exception, "The storage target of store {Store} failed its test: {Reason}", name, reason);

            throw new BusinessException(BusinessProblem.RuleViolation(
                "StorageTestFailed",
                "The storage target could not be used. The server log has the detail."));
        }
    }

    // An unanchored rule carries "RuleViolation" as its code and the rule as its source
    // (BusinessProblem.RuleViolation); an anchored one carries the rule as its code.
    static bool IsWordedHere(BusinessException refusal)
    {
        return refusal.Issues.Count > 0 && refusal.Issues.All(issue => issue.Code != "RuleViolation"
            || issue.Source is NotReadBack or ContentDiffers or NotListed);
    }

    static async Task Sweep(StorageManager storage, string key, CancellationToken cancellationToken)
    {
        try
        {
            await storage.Delete(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The target is already known to be broken; a failure to clean up after it adds
            // nothing the refusal above does not already say.
        }
    }
}
