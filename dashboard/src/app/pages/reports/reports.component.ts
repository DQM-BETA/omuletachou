import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';

import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';

import { ReportsService, ReportsTotals } from '../../core/services/reports.service';
import { QueueService, QueueItem } from '../../core/services/queue.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    NgChartsModule,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent implements OnInit {
  loading = true;
  errorMessage: string | null = null;

  totals: ReportsTotals | null = null;
  failedItems: QueueItem[] = [];
  retryingIds = new Set<string>();

  readonly failedColumns = ['socialNetwork', 'errorMessage', 'retryCount', 'actions'];

  barChartData: ChartData<'bar'> = { labels: [], datasets: [{ data: [], label: 'Publicacoes' }] };
  readonly barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { display: true } },
  };

  constructor(
    private reportsService: ReportsService,
    private queueService: QueueService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadReports();
  }

  loadReports(): void {
    this.loading = true;
    this.errorMessage = null;

    forkJoin({
      totals: this.reportsService.totals(),
      summary: this.reportsService.summary(),
      failed: this.queueService.list({ status: 'Failed', pageSize: 10 }),
    }).subscribe({
      next: ({ totals, summary, failed }) => {
        this.totals = totals;
        this.failedItems = failed.items;
        this.barChartData = {
          labels: summary.byNetwork.map((n) => n.network),
          datasets: [{ data: summary.byNetwork.map((n) => n.count), label: 'Publicacoes (7 dias)' }],
        };
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Erro ao carregar os relatorios.';
        this.loading = false;
      },
    });
  }

  retry(item: QueueItem): void {
    this.retryingIds.add(item.id);
    this.queueService.retry(item.id).subscribe({
      next: () => {
        this.retryingIds.delete(item.id);
        this.failedItems = this.failedItems.filter((i) => i.id !== item.id);
        this.snackBar.open('Item reenviado para a fila.', 'Fechar', { duration: 3000 });
      },
      error: () => {
        this.retryingIds.delete(item.id);
        this.snackBar.open('Erro ao tentar reenviar o item.', 'Fechar', { duration: 3000 });
      },
    });
  }

  isRetrying(id: string): boolean {
    return this.retryingIds.has(id);
  }
}
