// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useStrings } from "./context";
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
    <nav className="ns-pager">
      <span>{strings.format("Mud.MudDataGridPager_InfoFormat", [String(from), String(to), String(page.totalCount)])}</span>
      <NsButton text="Mud.MudTablePager_PreviousPage" variant="quiet" disabled={page.pageIndex <= 0} onClick={() => onPage(page.pageIndex - 1)} />
      <NsButton text="Mud.MudTablePager_NextPage" variant="quiet" disabled={page.pageIndex >= last} onClick={() => onPage(page.pageIndex + 1)} />
    </nav>
  );
}
