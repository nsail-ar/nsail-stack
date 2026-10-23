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
    if (!pending || !dialog.current) {
      return;
    }

    if (!dialog.current.open) {
      dialog.current.showModal();
    }

    // Which face the cursor opens on: an ordinary confirmation offers its own act, so the cursor
    // stands on it and Enter takes it; one that destroys stands on the way out, so Enter alone can
    // only decline (ui/forms.md, Focus on open).
    //
    // Placed here, after showModal(), and never by React's autoFocus: that prop focuses at the
    // commit that MOUNTED the faces, which is a commit where this dialog was still closed — a
    // display:none element taking no cursor — and it reaches the DOM as a focus() call, not as the
    // autofocus ATTRIBUTE a native dialog's own focusing steps read when showModal() runs (React
    // 19.3, read off a live DOM: neither face carries the attribute). Those steps are synchronous
    // inside the call above, so the cursor is placed after them rather than racing them, and the
    // placing holds whichever of the two mechanisms the browser answers.
    const [wayOut, act] = dialog.current.querySelectorAll<HTMLButtonElement>(".ns-actions button");

    (pending.danger ? wayOut : act).focus();
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
              <NsButton text="Actions.Confirm" variant={pending.danger ? "danger" : "primary"} onClick={() => close(true)} />
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
