// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Messaging.Runtime.Validation;

static class ValidatorChain
{
    // First refusal wins and short-circuits. A Problem carries one code and one title, so two
    // refusals cannot be merged into one honest answer, and a rule that already said no must
    // not make the caller pay for the next rule's query.
    public static async Task Guard<TMessage>(
        IReadOnlyList<IValidator<TMessage>> validators,
        TMessage message,
        CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            if (await validator.Validate(message, cancellationToken).ConfigureAwait(false) is { } problem)
            {
                throw new BusinessException(problem);
            }
        }
    }
}
