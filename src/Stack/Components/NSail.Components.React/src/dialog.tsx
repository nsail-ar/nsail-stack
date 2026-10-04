// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { NsButton } from "./button";

interface Pending {
  text: string;
  danger: boolean;
  resolve: (confirmed: boolean) => void;
}

const DialogContext = createContext<((text: string, danger?: boolean) => Promise<boolean>) | null>(null);

// One confirmation surface for the whole app, a native <dialog>: focus trapping, Escape and the
// backdrop come from the browser instead of from code this package would own.
export function NsDialogHost({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<Pending | null>(null);
  const dialog = useRef<HTMLDialogElement>(null);

  const confirm = useCallback((text: string, danger = false) => {
    return new Promise<boolean>((resolve) => setPending({ text, danger, resolve }));
  }, []);

  useEffect(() => {
    if (pending && dialog.current && !dialog.current.open) {
      dialog.current.showModal();
    }
  }, [pending]);

  function close(confirmed: boolean) {
    pending?.resolve(confirmed);
    dialog.current?.close();
    setPending(null);
  }

  return (
    <DialogContext.Provider value={confirm}>
      {children}
      <dialog ref={dialog} className="ns-dialog" onCancel={() => close(false)}>
        {pending && (
          <>
            <p>{pending.text}</p>
            <div className="ns-actions">
              <NsButton text="Actions.Cancel" onClick={() => close(false)} />
              <NsButton text="Actions.Confirm" variant={pending.danger ? "danger" : "primary"} onClick={() => close(true)} autoFocus />
            </div>
          </>
        )}
      </dialog>
    </DialogContext.Provider>
  );
}

export function useConfirm() {
  const confirm = useContext(DialogContext);

  if (!confirm) {
    throw new Error("useConfirm needs an <NsApp> above it.");
  }

  return confirm;
}
