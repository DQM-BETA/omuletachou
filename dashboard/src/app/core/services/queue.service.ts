import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { PagedResult, cleanParams } from './paged-result.model';

export type PublicationStatus = 'Scheduled' | 'Published' | 'Failed' | 'ManualPending';
export type SocialNetwork = 'Telegram' | 'Youtube' | 'Instagram' | 'TikTok' | 'Facebook';

export interface QueueItem {
  id: string;
  productId: string;
  socialNetwork: SocialNetwork;
  status: PublicationStatus;
  scheduledAt: string;
  publishedAt: string | null;
  retryCount: number;
  errorMessage: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class QueueService {
  constructor(private http: HttpClient) {}

  list(params: { status?: string; network?: string; page?: number; pageSize?: number }): Observable<PagedResult<QueueItem>> {
    return this.http.get<PagedResult<QueueItem>>('/api/queue', { params: new HttpParams({ fromObject: cleanParams(params) }) });
  }

  listManualPending(params: { page?: number; pageSize?: number }): Observable<PagedResult<QueueItem>> {
    return this.http.get<PagedResult<QueueItem>>('/api/queue/manual', { params: new HttpParams({ fromObject: cleanParams(params) }) });
  }

  retry(id: string): Observable<void> {
    return this.http.post<void>(`/api/queue/${id}/retry`, {});
  }

  markPublished(id: string): Observable<void> {
    return this.http.patch<void>(`/api/queue/${id}/status`, { status: 'Published' });
  }
}
