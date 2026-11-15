import { Provider } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

/** Paginator labels: "Rows per page" and a "1–10 of 24" range. Add to a page's providers. */
export function providePaginatorLabels(): Provider {
  return {
    provide: MatPaginatorIntl,
    useFactory: () => {
      const intl = new MatPaginatorIntl();
      intl.itemsPerPageLabel = 'Rows per page';
      intl.getRangeLabel = (page: number, pageSize: number, length: number) => {
        if (length === 0) {
          return `0 of 0`;
        }
        const start = page * pageSize + 1;
        const end = Math.min(start + pageSize - 1, length);
        return `${start}–${end} of ${length}`;
      };
      return intl;
    },
  };
}
