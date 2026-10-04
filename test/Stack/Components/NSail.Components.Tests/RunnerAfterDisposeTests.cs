// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Xunit;

namespace NSail.Components.Tests;

// What a Runner owes an act that outlived the component it belonged to. The ordering is not
// exotic: a send refused by the credential wall tears the page out from under its own submit
// (the auth state goes anonymous and the route view drops the subtree), and the submit's
// continuation then asks for one more run — NsForm's post-save refetch. Driven at the seam
// because no bUnit render can hold a handler open across its own component's disposal.
public sealed class RunnerAfterDisposeTests
{
    [Fact]
    public async Task A_run_asked_for_after_the_component_ended_does_nothing()
    {
        var runner = new Runner(new Host());
        var ran = false;

        runner.Dispose();

        await runner.Run(() =>
        {
            ran = true;

            return Task.CompletedTask;
        });

        Assert.False(ran);
    }

    // The submit's own shape, and the one that broke: the handler is in flight when the component
    // ends, and what runs AFTER the handler — outside the first run, not inside it, so no catch of
    // the Runner's own covers it — asks the same Runner for one more act.
    [Fact]
    public async Task An_act_that_outlived_its_component_finishes_without_throwing_at_its_caller()
    {
        var runner = new Runner(new Host());
        var answered = new TaskCompletionSource();
        var refetched = false;

        var submit = Submit();

        runner.Dispose();

        answered.SetResult();

        await submit;

        Assert.False(refetched);

        async Task Submit()
        {
            await runner.Run(() => answered.Task);

            await runner.Run(() =>
            {
                refetched = true;

                return Task.CompletedTask;
            });
        }
    }

    sealed class Host : IRunHost
    {
        public SurfaceContext? Surface
        {
            get { return null; }
        }

        public object Source
        {
            get { return this; }
        }

        public Task Report(ProblemEventArgs args)
        {
            return Task.CompletedTask;
        }

        public void StateChanged()
        {
        }
    }
}
