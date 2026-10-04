// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Takes both ends into the member's own model: what a nested model declares refuses
/// on the wire (<see cref="MessageValidator"/>) and draws on screen
/// (NsDataAnnotationsValidator) exactly like the message's own annotations, with the field
/// named by its path from the message ("Right.Sphere") so a form still finds the input it
/// belongs under.
///
/// Opt-in per member, and deliberately so: neither end walks a nested model otherwise, and a
/// declaration nobody enforces must not be drawn as though it were — a row model (a sale line,
/// a channel) reaches nothing until the member holding it says so.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidatedAttribute : Attribute
{
}
