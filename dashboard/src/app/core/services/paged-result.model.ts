import { HttpParams } from '@angular/common/http';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

/**
 * Remove chaves undefined/null/vazias antes de montar HttpParams — evita enviar
 * `status=&platform=` para a API (especificacao-tecnica.md §4).
 */
export function cleanParams(params: object): HttpParams {
  let httpParams = new HttpParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    httpParams = httpParams.set(key, value);
  }
  return httpParams;
}
