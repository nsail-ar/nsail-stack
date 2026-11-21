// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useStrings } from "@nsail/stack";
import { NsButton } from "./button";

export interface PageInfo {
  pageIndex: number;
  pageSize: number;
  totalCount: number;
}

export function NsPager({ page, onPage }: { page: PageInfo; onPage: (pageIndex: number) => void }) {
  const strings = useStrings();
  const from = page.totalCount === 0 ? 0 : page.pageIndex * page.pageSize + 1;
  const to = Math.min((page.pageIndex + 1) * page.pageSize, page.totalCount);
  const last = Math.max(0, Math.ceil(page.totalCount / page.pageSize) - 1);

  return (
    <Stack component="nav" direction="row" spacing={1} sx={{ alignItems: "center", justifyContent: "flex-end", pt: 1 }}>
      <Typography variant="body2" color="text.secondary">
        {strings.format("Mud.MudDataGridPager_InfoFormat", [String(from), String(to), String(page.totalCount)])}
      </Typography>
      <NsButton text="Mud.MudTablePager_PreviousPage" variant="quiet" disabled={page.pageIndex <= 0} onClick={() => onPage(page.pageIndex - 1)} />
      <NsButton text="Mud.MudTablePager_NextPage" variant="quiet" disabled={page.pageIndex >= last} onClick={() => onPage(page.pageIndex + 1)} />
    </Stack>
  );
}
