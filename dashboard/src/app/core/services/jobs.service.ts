import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type JobKind =
  | 'collector'
  | 'collector-amazon'
  | 'collector-mercadolivre'
  | 'collector-shopee'
  | 'processor'
  | 'publisher';

export const JOB_ENDPOINTS: Record<JobKind, string> = {
  collector: '/api/jobs/collector/trigger',
  'collector-amazon': '/api/jobs/collector/amazon/trigger',
  'collector-mercadolivre': '/api/jobs/collector/mercadolivre/trigger',
  'collector-shopee': '/api/jobs/collector/shopee/trigger',
  processor: '/api/jobs/processor/trigger',
  publisher: '/api/jobs/publisher/trigger',
};

@Injectable({ providedIn: 'root' })
export class JobsService {
  constructor(private http: HttpClient) {}

  trigger(kind: JobKind): Observable<{ count?: number }> {
    return this.http.post<{ count?: number }>(JOB_ENDPOINTS[kind], {});
  }
}
