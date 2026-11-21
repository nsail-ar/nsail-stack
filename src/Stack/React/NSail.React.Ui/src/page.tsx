// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useEffect, type ReactNode } from "react";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useStrings } from "@nsail/stack";

export interface NsPageProps {
  /** A string key. */
  title: string;
  actions?: ReactNode;
  children: ReactNode;
}

export function NsPage({ title, actions, children }: NsPageProps) {
  const strings = useStrings();
  const text = strings.translate(title);

  useEffect(() => {
    document.title = text;
  }, [text]);

  return (
    <Container component="main" maxWidth="lg" sx={{ pt: 3, pb: 6 }}>
      <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", mb: 2 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 600 }}>
          {text}
        </Typography>
        {actions && (
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
            {actions}
          </Stack>
        )}
      </Stack>
      <Stack spacing={2}>{children}</Stack>
    </Container>
  );
}
