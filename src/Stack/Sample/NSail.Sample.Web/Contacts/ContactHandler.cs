// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data;
using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Paging;
using NSail.Problems;
using NSail.Sample.Contacts.Models;
using NSail.Sample.Entities;

namespace NSail.Sample.Contacts;

[Injectable]
public sealed class ContactHandler :
    IHandler<ListContacts, DataPage<ContactRow>>,
    IHandler<GetContact, ContactModel>,
    IHandler<CreateContact>,
    IHandler<UpdateContact>,
    IHandler<DeleteContact>
{
    readonly DbContext _db;

    public ContactHandler(DbContext db)
    {
        _db = db;
    }

    public async Task<DataPage<ContactRow>> Handle(ListContacts message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var query = _db.Set<Contact>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(message.Search))
        {
            var term = message.Search.Trim();

            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        if (message.Kind is { } kind)
        {
            query = query.Where(c => c.Kind == kind);
        }

        var pageSize = message.PageSize <= 0 ? 10 : message.PageSize;
        var pageIndex = message.PageIndex < 0 ? 0 : message.PageIndex;

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(c => new ContactRow
            {
                Id = c.Id,
                Name = c.Name,
                Kind = c.Kind,
                Email = c.Email,
                Phone = c.Phone,
                Rating = c.Rating,
                IsFavorite = c.IsFavorite,
                UpdatedAt = c.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new DataPage<ContactRow>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ContactModel> Handle(GetContact message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var contact = await Find(message.Id, cancellationToken);

        return new ContactModel
        {
            Id = contact.Id,
            Version = contact.Version,
            Name = contact.Name,
            Kind = contact.Kind,
            Email = contact.Email,
            Phone = contact.Phone,
            BirthDate = contact.BirthDate,
            Rating = contact.Rating,
            IsFavorite = contact.IsFavorite,
            Notes = contact.Notes,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
        };
    }

    public async Task Handle(CreateContact message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (await _db.Set<Contact>().AnyAsync(c => c.Id == message.Id, cancellationToken))
        {
            throw new BusinessException(BusinessProblem.AlreadyExists(typeof(Contact), message.Id));
        }

        var now = DateTime.UtcNow;

        _db.Add(new Contact
        {
            Id = message.Id,
            Name = message.Name.Trim(),
            Kind = message.Kind,
            Email = message.Email,
            Phone = message.Phone,
            BirthDate = message.BirthDate,
            Rating = message.Rating,
            IsFavorite = message.IsFavorite,
            Notes = message.Notes,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(UpdateContact message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var contact = await Find(message.Id, cancellationToken);

        _db.ExpectVersion(contact, message.Version);

        contact.Name = message.Name.Trim();
        contact.Kind = message.Kind;
        contact.Email = message.Email;
        contact.Phone = message.Phone;
        contact.BirthDate = message.BirthDate;
        contact.Rating = message.Rating;
        contact.IsFavorite = message.IsFavorite;
        contact.Notes = message.Notes;
        contact.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteContact message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var contact = await Find(message.Id, cancellationToken);

        _db.Remove(contact);

        await _db.SaveChangesAsync(cancellationToken);
    }

    async Task<Contact> Find(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Set<Contact>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new BusinessException(BusinessProblem.NotFound(typeof(Contact), id));
    }
}
