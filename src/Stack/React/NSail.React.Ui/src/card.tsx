// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import Card from "@mui/material/Card";
import CardActions from "@mui/material/CardActions";
import CardContent from "@mui/material/CardContent";
import CardHeader from "@mui/material/CardHeader";
import { useStrings } from "@nsail/stack";

export interface NsCardProps {
  /** A string key: the card's heading. */
  title?: string;
  /** A string key: the line under the heading. */
  subtitle?: string;
  /** Drawn beside the heading: an NsChip, an NsButton. */
  aside?: ReactNode;
  /** The card's own acts, under its content. */
  actions?: ReactNode;
  children?: ReactNode;
}

/** A surface that groups one subject: an optional heading, its content and its acts. */
export function NsCard({ title, subtitle, aside, actions, children }: NsCardProps) {
  const strings = useStrings();

  return (
    <Card variant="outlined" component="section">
      {title && (
        <CardHeader
          title={strings.translate(title)}
          subheader={subtitle ? strings.translate(subtitle) : undefined}
          action={aside}
          slotProps={{ title: { variant: "h6", component: "h2" } }}
        />
      )}
      {children && <CardContent sx={title ? { pt: 0 } : undefined}>{children}</CardContent>}
      {actions && <CardActions sx={{ justifyContent: "flex-end", px: 2, pb: 2 }}>{actions}</CardActions>}
    </Card>
  );
}
