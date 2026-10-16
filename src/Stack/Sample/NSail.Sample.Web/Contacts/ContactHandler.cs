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
    readonly EntityMapper _mapper;

    public ContactHandler(DbContext db, EntityMapper mapper)
    {
        _db = db;
        _mapper = mapper;
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

        var page = query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize);

        var items = await _mapper.Project<Contact, ContactRow>(page).ToListAsync(cancellationToken);

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

        var query = _db.Set<Contact>().AsNoTracking().Where(c => c.Id == message.Id);

        return await _mapper.Project<Contact, ContactModel>(query).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(BusinessProblem.NotFound(typeof(Contact), message.Id));
    }

    public async Task Handle(CreateContact message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (await _db.Set<Contact>().AnyAsync(c => c.Id == message.Id, cancellationToken))
        {
            throw new BusinessException(BusinessProblem.AlreadyExists(typeof(Contact), message.Id));
        }

        var contact = await _mapper.Upsert<CreateContact, Contact>(message, cancellationToken);

        contact.Name = contact.Name.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(UpdateContact message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var contact = await _mapper.Map<UpdateContact, Contact>(message, cancellationToken);

        contact.Name = contact.Name.Trim();

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
