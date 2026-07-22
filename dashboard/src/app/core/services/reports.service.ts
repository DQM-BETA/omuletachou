import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ReportsSummary {
  periodStart: string;
  periodEnd: string;
  totalPublished: number;
  byNetwork: { network: string; count: number }[];
  byDay: { date: string; count: number }[];
}

export interface ReportsTotals {
  today: number;
  week: number;
  month: number;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  constructor(private http: HttpClient) {}

  summary(): Observable<ReportsSummary> {
    return this.http.get<ReportsSummary>('/api/reports/summary');
  }

  totals(): Observable<ReportsTotals> {
    return this.http.get<ReportsTotals>('/api/reports/totals');
  }

  // Falhas recentes: reaproveita QueueService.list({ status: 'Failed' }) — sem endpoint novo.
}
