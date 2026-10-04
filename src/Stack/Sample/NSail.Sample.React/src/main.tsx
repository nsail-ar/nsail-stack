// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router";
import { Mediator } from "@nsail/messaging";
import { NsApp } from "@nsail/components";
import "@nsail/components/ns.css";
import { GetStrings } from "./sdk";
import { routes } from "./routes";
import { ContactsPage } from "./contacts/ContactsPage";
import { ContactPage } from "./contacts/ContactPage";

const mediator = new Mediator();

// The host answers its catalog for any language it carries and its default for any other, so
// the browser's own preference is all the client has to name.
const language = navigator.language.split("-")[0] || "es";

function strings(lang: string) {
  return mediator.send(GetStrings, { language: lang });
}

// The build's base is the router's: Vite hands it over with a trailing slash the router does not
// want, and the routes stay written from the client's own root.
const router = createBrowserRouter(
  [
    { path: routes.contacts.pattern, element: <ContactsPage /> },
    { path: routes.newContact.pattern, element: <ContactPage /> },
    { path: routes.contact.pattern, element: <ContactPage /> },
  ],
  { basename: import.meta.env.BASE_URL.replace(/\/$/, "") },
);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <NsApp mediator={mediator} language={language} strings={strings}>
      <RouterProvider router={router} />
    </NsApp>
  </StrictMode>,
);
