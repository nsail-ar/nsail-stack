// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// What a provider adds to the environment of the processes the harness starts: a lane's scripts get
// its credentials, a seat gets none — and is fenced off from any it might find on the machine.
public interface ILaneEnvironment
{
    void Lane(IDictionary<string, string> env);

    void Seat(IDictionary<string, string> env);

    // A configured command about to run, its HARNESS_WORKSPACE (and HARNESS_TEMP) set: the names the
    // provider's own CI gives the same things, for scripts that also run there.
    void Command(IDictionary<string, string> env)
    {
    }
}
