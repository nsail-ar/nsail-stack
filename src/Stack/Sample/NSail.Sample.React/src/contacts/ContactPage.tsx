// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useEffect } from "react";
import { useNavigate, useParams } from "react-router";
import { useForm, useLoad, useMediator, type Form } from "@nsail/stack";
import { NsButton, NsField, NsForm, NsLoad, NsPage, NsSaved } from "@nsail/ui";
import { CreateContact, GetContact, UpdateContact, type ContactModel } from "../sdk";
import { routes } from "../routes";

// One page, two messages: a new contact is CreateContact, an existing one UpdateContact — the
// contracts differ, so the forms do too, and only their shared fields are drawn by one piece.
export function ContactPage() {
  const { id } = useParams();

  return id ? <EditContact id={id} /> : <NewContact />;
}

function Fields<T extends CreateContact | UpdateContact>({ form }: { form: Form<T> }) {
  const navigate = useNavigate();

  return (
    <>
      <NsField<T> name="name" autoFocus />
      <NsField<T> name="kind" />
      <NsField<T> name="email" />
      <NsField<T> name="phone" />
      <NsField<T> name="birthDate" />
      <NsField<T> name="rating" />
      <NsField<T> name="isFavorite" />
      <div className="ns-field-wide">
        <NsField<T> name="notes" multiline />
      </div>
      <div className="ns-actions">
        <NsButton text="Common.Submit" type="submit" variant="primary" busy={form.submitting} />
        <NsButton text="Common.Back" onClick={() => navigate(routes.contacts.href())} />
        <NsSaved />
      </div>
    </>
  );
}

function NewContact() {
  const mediator = useMediator();
  const navigate = useNavigate();
  const form = useForm(CreateContact, { id: crypto.randomUUID(), name: "", kind: "Person", rating: 0, isFavorite: false });

  // Master/detail: a create that lands becomes the record's edit page, in place.
  async function save(value: CreateContact) {
    await mediator.send(CreateContact, value);
    navigate(routes.contact.href({ id: value.id }), { replace: true });
  }

  return (
    <NsPage title="Sample.CreateContactPage.Title">
      <div className="ns-card">
        <NsForm form={form} onSubmit={save}>
          <Fields form={form} />
        </NsForm>
      </div>
    </NsPage>
  );
}

function EditContact({ id }: { id: string }) {
  const contact = useLoad(GetContact, { id });

  return (
    <NsPage title="Sample.UpdateContactPage.Title">
      <NsLoad load={contact}>{(model) => <EditForm model={model} reload={contact.reload} />}</NsLoad>
    </NsPage>
  );
}

function toUpdate(model: ContactModel): UpdateContact {
  return {
    id: model.id,
    version: model.version,
    name: model.name,
    kind: model.kind,
    email: model.email,
    phone: model.phone,
    birthDate: model.birthDate,
    rating: model.rating,
    isFavorite: model.isFavorite,
    notes: model.notes,
  };
}

function EditForm({ model, reload }: { model: ContactModel; reload: () => void }) {
  const mediator = useMediator();
  const form = useForm(UpdateContact, toUpdate(model));
  const { reset } = form;

  // Saving keeps the page open and re-reads it: what is shown is what the server holds.
  useEffect(() => {
    reset(toUpdate(model));
  }, [model, reset]);

  async function save(value: UpdateContact) {
    await mediator.send(UpdateContact, value);
    reload();
  }

  return (
    <div className="ns-card">
      <NsForm form={form} onSubmit={save} onReload={reload}>
        <Fields form={form} />
      </NsForm>
    </div>
  );
}
