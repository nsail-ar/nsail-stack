// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";

/** Rows of one kind, one under the other. */
export function NsList({ children }: { children: ReactNode }) {
  return <List disablePadding>{children}</List>;
}

export interface NsListItemProps {
  /** What the row is: data in hand (a device, an address), never a sentence typed in a page. */
  primary: ReactNode;
  secondary?: ReactNode;
  icon?: ReactNode;
  /** Drawn at the row's end: an NsChip, an NsButton. */
  aside?: ReactNode;
  /** The row opens something: an address of the client's route table. */
  href?: string;
  onClick?: () => void;
}

/** One row of an NsList; it opens when it has an href or an onClick, from the keyboard too. */
export function NsListItem({ primary, secondary, icon, aside, href, onClick }: NsListItemProps) {
  const content = (
    <>
      {icon && <ListItemIcon>{icon}</ListItemIcon>}
      <ListItemText primary={primary} secondary={secondary} />
    </>
  );

  if (href || onClick) {
    return (
      <ListItem disablePadding divider secondaryAction={aside}>
        {href ? (
          <ListItemButton component="a" href={href} onClick={onClick}>
            {content}
          </ListItemButton>
        ) : (
          <ListItemButton onClick={onClick}>{content}</ListItemButton>
        )}
      </ListItem>
    );
  }

  return (
    <ListItem divider secondaryAction={aside}>
      {content}
    </ListItem>
  );
}
