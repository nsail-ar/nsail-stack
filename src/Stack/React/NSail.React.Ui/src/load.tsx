// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import { isRetryable, useStrings, type Load } from "@nsail/stack";
import { NsButton } from "./button";

export interface NsLoadProps<T> {
  load: Load<T>;
  children: (data: T) => ReactNode;
}

/** A region that owns its read: a spinner the first time, the last answer (dimmed) while it
 * re-reads, and a failure in place with a retry where a second try can pass — never a toast,
 * never an empty state that lies about a read that failed. */
export function NsLoad<T>({ load, children }: NsLoadProps<T>) {
  const strings = useStrings();

  if (load.state === "failed") {
    return (
      <Alert
        severity="error"
        role="alert"
        action={isRetryable(load.problem) ? <NsButton text="Common.Retry" variant="quiet" onClick={load.reload} /> : undefined}
      >
        <AlertTitle>{strings.problem(load.problem)}</AlertTitle>
        {load.problem.issues.map((issue, index) => (
          <div key={index}>{strings.issue(issue)}</div>
        ))}
      </Alert>
    );
  }

  if (load.data === undefined) {
    return (
      <Box aria-busy="true" sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress aria-label={strings.translate("Common.Loading")} />
      </Box>
    );
  }

  return (
    <Box aria-busy={load.state === "loading" || undefined} sx={{ opacity: load.state === "loading" ? 0.6 : 1, transition: "opacity 120ms" }}>
      {children(load.data)}
    </Box>
  );
}
