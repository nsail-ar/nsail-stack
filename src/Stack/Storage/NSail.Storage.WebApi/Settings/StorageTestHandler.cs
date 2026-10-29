// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Problems;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using System.Text;

namespace NSail.Storage.Settings;

// The whole round trip against the saved target, before anything depends on it: write a probe,
// read it back, prove the bytes survived, list it, remove it. A test that only wrote would pass
// against a bucket the install cannot read from, and one that left its probe behind would
// litter the container it just proved.
//
// A probe that fails is a REFUSAL and answers like one — nothing is caught into a Succeeded flag.
// The anchored refusals underneath travel untouched: StorageManager already names
// Proveedor when the install stores nowhere, which is a field this screen renders, so the "no"
// lands on it instead of in a toast. The probe's own verdicts name no field and fall to the foot
// of the form, each under a rule of its own so each is worded in the install's language.
[Injectable]
public class StorageTestHandler : IHandler<TestStorageTarget>
{
    readonly StorageStores _stores;
    readonly StorageCredentialProvider _credentials;
    readonly IEnumerable<IStorageProvider> _providers;

    public StorageTestHandler(StorageStores stores, StorageCredentialProvider credentials, IEnumerable<IStorageProvider> providers)
    {
        _stores = stores;
        _credentials = credentials;
        _providers = providers;
    }

    public async Task Handle(TestStorageTarget message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The name's own manager, so the probe lands where that name's consumer writes — its own
        // target, or the default's where it has none.
        var storage = new StorageManager(_stores.Require(message.Name), _credentials, _providers);
        var key = $"nsail-storage-test/{Guid.NewGuid():N}.txt";
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
                    "StorageTestNotReadBack",
                    "The object was written but could not be read back."));
            }

            using var readBack = new MemoryStream();
            await stored.Content.CopyToAsync(readBack, cancellationToken);

            if (!readBack.ToArray().AsSpan().SequenceEqual(payload))
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    "StorageTestContentDiffers",
                    "The object read back does not match what was written."));
            }

            var listed = await storage.List("nsail-storage-test/", cancellationToken);

            if (!listed.Any(entry => entry.Key == key))
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    "StorageTestNotListed",
                    "The object was written but does not appear when the container is listed."));
            }

            await storage.Delete(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Swept whatever the verdict was, so a target that half-worked is not left holding the
            // probe — and the refusal goes on up: a BusinessException from underneath already says
            // what is wrong and where (StorageManager), and anything else is this rule saying it.
            await Sweep(storage, key, cancellationToken);

            if (exception is BusinessException)
            {
                throw;
            }

            throw new BusinessException(BusinessProblem.RuleViolation(
                "StorageTestFailed",
                $"The storage target could not be used: {exception.Message}",
                new Dictionary<string, string> { ["reason"] = exception.Message }));
        }
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
