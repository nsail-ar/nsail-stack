// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useMemo, type ReactNode } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import CssBaseline from "@mui/material/CssBaseline";
import useMediaQuery from "@mui/material/useMediaQuery";
import { ThemeProvider } from "@mui/material/styles";
import { StackProvider, useCatalog, type Brand, type Mediator, type MessageType, type SessionShape } from "@nsail/stack";
import { NsDialogHost } from "./dialog";
import { brandTheme } from "./theme";

export interface NsAppProps {
  mediator: Mediator;
  language: string;
  /** The app's catalog for a language, merged over the Stack's. */
  strings?: (language: string) => Promise<Record<string, string>>;
  /** The message that answers who is signed in, read for useSession and NsSignedIn. */
  session?: MessageType<Record<never, never>, SessionShape>;
  /** The install's branding: colours, typeface and both schemes. None draws the Stack's own. */
  brand?: Brand | null;
  /** The scheme to draw. Unset follows the device. */
  dark?: boolean;
  children: ReactNode;
}

/** The app's root: the stack's providers, the theme from the brand, the catalog read before the
 * first paint, and the one confirmation surface. */
export function NsApp({ mediator, language, strings, session, brand, dark, children }: NsAppProps) {
  const catalog = useCatalog(language, strings);
  const device = useMediaQuery("(prefers-color-scheme: dark)", { noSsr: true });
  const scheme = dark ?? device;
  const theme = useMemo(() => brandTheme(brand, scheme), [brand, scheme]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline enableColorScheme />
      {catalog.strings ? (
        <StackProvider mediator={mediator} strings={catalog.strings} session={session}>
          {catalog.failed && (
            <Alert severity="error" variant="filled" square>
              {catalog.strings.translate("Common.UnhandledError")}
            </Alert>
          )}
          <NsDialogHost>{children}</NsDialogHost>
        </StackProvider>
      ) : (
        <Box aria-busy="true" sx={{ minHeight: "100vh" }} />
      )}
    </ThemeProvider>
  );
}
