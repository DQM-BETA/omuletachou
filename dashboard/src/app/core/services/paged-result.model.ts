import { HttpParams } from '@angular/common/http';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

/**
 * Remove chaves undefined/null/vazias de um objeto de params antes de montar
 * o HttpParams, evitando enviar query strings do tipo "status=&platform=".
 */
export function cleanParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') {
      continue;
    }
    httpParams = httpParams.set(key, String(value));
  }
  return httpParams;
}
