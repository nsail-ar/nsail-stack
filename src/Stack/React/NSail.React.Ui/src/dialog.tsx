// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { styled } from "@mui/material/styles";
import { NsButton } from "./button";

interface Pending {
  text: string;
  danger: boolean;
  resolve: (confirmed: boolean) => void;
}

// The browser's own <dialog>, worn in the theme: MUI's Dialog would bring a focus trap of its
// own, and the one this surface keeps is the platform's.
const Surface = styled("dialog")(({ theme }) => ({
  border: "none",
  borderRadius: Number(theme.shape.borderRadius) * 2,
  padding: theme.spacing(3),
  maxWidth: "min(480px, calc(100vw - 32px))",
  background: theme.palette.background.raised,
  color: theme.palette.text.primary,
  boxShadow: theme.shadows[8],
  "&::backdrop": { background: "rgb(0 0 0 / 45%)" },
}));

const DialogContext = createContext<((text: string, danger?: boolean) => Promise<boolean>) | null>(null);

// One confirmation surface for the whole app, a native <dialog>: focus trapping, Escape and the
// backdrop come from the browser instead of from code this package would own.
export function NsDialogHost({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<Pending | null>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  const open = useRef<Pending | null>(null);

  // A confirmation asked while another is open ends the first as declined: no caller waits forever.
  const confirm = useCallback((text: string, danger = false) => {
    return new Promise<boolean>((resolve) => {
      open.current?.resolve(false);
      open.current = { text, danger, resolve };
      setPending(open.current);
    });
  }, []);

  useEffect(() => {
    return () => open.current?.resolve(false);
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
    const [wayOut, act] = dialog.current.querySelectorAll<HTMLButtonElement>("button");

    (pending.danger ? wayOut : act).focus();
  }, [pending]);

  function close(confirmed: boolean) {
    open.current?.resolve(confirmed);
    open.current = null;
    dialog.current?.close();
    setPending(null);
  }

  return (
    <DialogContext.Provider value={confirm}>
      {children}
      <Surface ref={dialog} onCancel={() => close(false)}>
        {pending && (
          <>
            <Typography sx={{ mb: 3 }}>{pending.text}</Typography>
            <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
              <NsButton text="Actions.Cancel" onClick={() => close(false)} />
              <NsButton text="Actions.Confirm" variant={pending.danger ? "danger" : "primary"} onClick={() => close(true)} />
            </Stack>
          </>
        )}
      </Surface>
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
