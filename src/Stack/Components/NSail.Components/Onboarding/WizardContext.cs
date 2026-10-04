// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Where a wizard stands, cascaded to the step on screen. A step says at most two
/// things to it — that it participates, and whether it is ready — and the chrome does the
/// rest: it owns Next and Back, so no step renders its own way forward.</summary>
public sealed class WizardContext
{
    readonly HashSet<int> _completed = [];

    Func<Task<bool>>? _onNext;

    bool _canAdvance = true;

    bool _advancing;

    public WizardContext(IReadOnlyList<OnboardingStep> steps, int index)
    {
        ArgumentNullException.ThrowIfNull(steps);

        Steps = steps;
        Index = Math.Clamp(index, 0, Math.Max(steps.Count - 1, 0));

        // Everything the sequence opened past reported itself done: FirstPending walks the
        // steps in order and stops at the first that did not, so the marks and the landing
        // step come from one answer rather than two that could disagree.
        for (var behind = 0; behind < index && behind < steps.Count; behind++)
        {
            _completed.Add(behind);
        }
    }

    public IReadOnlyList<OnboardingStep> Steps { get; }

    public int Index { get; private set; }

    public OnboardingStep? Current => Index < Steps.Count ? Steps[Index] : null;

    /// <summary>Whether the chrome's Next may fire. True until a step says otherwise — an
    /// informational step is born complete and never has to ask for its own button.</summary>
    public bool CanAdvance => _canAdvance;

    /// <summary>Whether a Next is in flight. The step's own hook is usually its form's
    /// Submit(), so a second Next while the first is running re-enters that form's Runner and
    /// earns its refusal — the sequence owns Next, so the sequence is what says no. It is its
    /// own word and not a CanAdvance the chrome writes: a step that cannot advance must still
    /// be able to go Back, and this is the one moment neither way is offered.</summary>
    public bool IsAdvancing => _advancing;

    public bool IsFirst => Index <= 0;

    public bool IsLast => Index >= Steps.Count - 1;

    public event Action? Changed;

    public bool IsComplete(int index)
    {
        return _completed.Contains(index);
    }

    /// <summary>The step's say on the way out: it may do work, and it may refuse. Answering
    /// false holds the step on screen with whatever it drew, which is how a form step keeps a
    /// refusal under its own field. A step that registers nothing simply advances.</summary>
    public void Participate(Func<Task<bool>> onNext)
    {
        ArgumentNullException.ThrowIfNull(onNext);

        _onNext = onNext;
    }

    public void SetCanAdvance(bool canAdvance)
    {
        if (_canAdvance == canAdvance)
        {
            return;
        }

        _canAdvance = canAdvance;

        Changed?.Invoke();
    }

    public async Task Next()
    {
        if (!_canAdvance || _advancing)
        {
            return;
        }

        _advancing = true;

        // Said before the hook runs, so the chrome greys both of its buttons for the length of
        // it rather than after: what the hook is doing is usually a form's submit, and the
        // window where the second press lands is exactly the one it is open.
        Changed?.Invoke();

        try
        {
            if (_onNext is { } onNext && !await onNext())
            {
                return;
            }

            _completed.Add(Index);

            // The last step has nowhere to go and its participation must survive: resetting
            // here would drop the hook of a step that is still mounted and will never
            // re-register it.
            if (!IsLast)
            {
                Index++;

                Reset();
            }
        }
        finally
        {
            _advancing = false;

            // In the finally and not after it: a veto returns from inside the try, and a Next
            // left greyed over a step that refused is a wizard nobody can leave.
            Changed?.Invoke();
        }
    }

    /// <summary>One step back — and the step remounts, which is what reloads it from the
    /// server: a step's own load runs on arrival, so nothing here has to know how to refetch
    /// anybody's data.</summary>
    public void Back()
    {
        // Leaving mid-flight unmounts the step the hook is still submitting, which is the same
        // press-while-saving the Next guard above refuses — one save, one way out of it.
        if (IsFirst || _advancing)
        {
            return;
        }

        Index--;

        Reset();

        Changed?.Invoke();
    }

    void Reset()
    {
        _onNext = null;
        _canAdvance = true;
    }
}
