// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Problems;

// A namespace the default metadata template binds the way a kit's does — Area = Consulting,
// Feature = Patients — so the span names these tests assert are the ones a real message
// would produce, not a shape invented for the harness.
namespace NSail.Consulting.Patients;

public sealed record CreatePatient(string Name, string Document, string Notes) : IMessage<Guid>;

public sealed record ArchivePatient(Guid Id) : IMessage;

public sealed record RefusedPatient : IMessage;

public sealed class CreatePatientHandler : IHandler<CreatePatient, Guid>
{
    public Func<Task>? Nested { get; set; }

    public async Task<Guid> Handle(CreatePatient message, CancellationToken cancellationToken)
    {
        if (Nested is not null)
        {
            await Nested();
        }

        return Guid.NewGuid();
    }
}

public sealed class ArchivePatientHandler : IHandler<ArchivePatient>
{
    public Action? OnHandle { get; set; }

    public Task Handle(ArchivePatient message, CancellationToken cancellationToken)
    {
        OnHandle?.Invoke();

        return Task.CompletedTask;
    }
}

public sealed class RefusedPatientHandler : IHandler<RefusedPatient>
{
    public const string Code = "PatientRefused";

    public Task Handle(RefusedPatient message, CancellationToken cancellationToken)
    {
        throw new BusinessException(new Problem(Code, "Juan Perez may not be archived", []));
    }
}
