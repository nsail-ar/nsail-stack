// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { MouseEvent, ReactNode } from "react";
import Box from "@mui/material/Box";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import { useStrings } from "@nsail/stack";

export interface NsNavEntry {
  /** A string key: the entry's name. */
  text: string;
  /** An address of the client's route table. */
  href: string;
  icon?: ReactNode;
}

export interface NsNavMenuProps {
  /** A string key naming the navigation for a screen reader. */
  label: string;
  entries: readonly NsNavEntry[];
  /** The href of the entry the reader is on. */
  current?: string;
  /** A client router's navigation; without it an entry is a plain link. */
  onNavigate?: (href: string) => void;
}

/** The side navigation: a band in the brand's chrome, the entry the reader is on marked for
 * the eye and for a screen reader alike. */
export function NsNavMenu({ label, entries, current, onNavigate }: NsNavMenuProps) {
  const strings = useStrings();

  function follow(event: MouseEvent, href: string) {
    if (!onNavigate || event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) {
      return;
    }

    event.preventDefault();
    onNavigate(href);
  }

  return (
    <Box component="nav" aria-label={strings.translate(label)} sx={{ bgcolor: "chrome.main", color: "chrome.contrastText", height: "100%" }}>
      <List>
        {entries.map((entry) => {
          const here = entry.href === current;

          return (
            <ListItemButton
              key={entry.href}
              component="a"
              href={entry.href}
              selected={here}
              aria-current={here ? "page" : undefined}
              onClick={(event: MouseEvent) => follow(event, entry.href)}
              sx={{ borderRadius: 1, mx: 1, color: "inherit", "&.Mui-selected, &.Mui-selected:hover": { color: "chrome.accent", bgcolor: "chrome.selected" } }}
            >
              {entry.icon && <ListItemIcon sx={{ color: here ? "chrome.accent" : "chrome.secondary", minWidth: 36 }}>{entry.icon}</ListItemIcon>}
              <ListItemText primary={strings.translate(entry.text)} />
            </ListItemButton>
          );
        })}
      </List>
    </Box>
  );
}
