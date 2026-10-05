// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useCallback, useState } from "react";
import { useNavigate } from "react-router";
import { useLoad, useMediator, useStrings } from "@nsail/stack";
import { NsButton, NsEnumSelect, NsLoad, NsPage, NsPager, NsSearch, NsTable, useConfirm } from "@nsail/ui";
import { ContactRow, DeleteContact, ListContacts, type ContactKind } from "../sdk";
import { routes } from "../routes";

export function ContactsPage() {
  const mediator = useMediator();
  const strings = useStrings();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const [filter, setFilter] = useState<ListContacts>({ pageIndex: 0, pageSize: 10 });
  const contacts = useLoad(ListContacts, filter);

  const search = useCallback((term: string | null) => {
    setFilter((current) => ({ ...current, search: term, pageIndex: 0 }));
  }, []);

  async function remove(row: ContactRow) {
    if (!(await confirm(strings.format("Sample.ContactsPage.DeleteConfirm", { name: row.name }), true))) {
      return;
    }

    await mediator.send(DeleteContact, { id: row.id });
    contacts.reload();
  }

  return (
    <NsPage
      title="Sample.ContactsPage.Title"
      actions={<NsButton text="Common.New" variant="primary" onClick={() => navigate(routes.newContact.href())} />}
    >
      <div className="ns-toolbar">
        <NsSearch field={ListContacts.fields.search} value={filter.search} onSearch={search} />
        <NsEnumSelect<ContactKind>
          field={ListContacts.fields.kind}
          value={filter.kind}
          onChange={(kind) => setFilter((current) => ({ ...current, kind, pageIndex: 0 }))}
        />
      </div>
      <NsLoad load={contacts}>
        {(page) => (
          <>
            <NsTable
              model={ContactRow}
              rows={page.items}
              rowKey="id"
              columns={["name", "kind", "email", "phone", "rating", "isFavorite", "updatedAt"]}
              empty="Sample.ContactsPage.Empty"
              onRowClick={(row) => navigate(routes.contact.href({ id: row.id }))}
              actions={(row) => <NsButton text="Actions.Delete" variant="quiet" onClick={() => void remove(row)} />}
            />
            <NsPager page={page} onPage={(pageIndex) => setFilter((current) => ({ ...current, pageIndex }))} />
          </>
        )}
      </NsLoad>
    </NsPage>
  );
}
