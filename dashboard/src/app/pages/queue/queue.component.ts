import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { QueueService, QueueItem, PublicationStatus, SocialNetwork } from '../../core/services/queue.service';

@Component({
  selector: 'app-queue',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './queue.component.html',
  styleUrl: './queue.component.scss',
})
export class QueueComponent implements OnInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  readonly displayedColumns = ['socialNetwork', 'status', 'scheduledAt', 'publishedAt', 'retryCount', 'error', 'actions'];

  readonly statuses: PublicationStatus[] = ['Scheduled', 'Published', 'Failed', 'ManualPending'];
  readonly networks: SocialNetwork[] = ['Telegram', 'Youtube', 'Instagram', 'TikTok', 'Facebook'];

  dataSource = new MatTableDataSource<QueueItem>([]);
  totalItems = 0;
  pageIndex = 0;
  pageSize = 20;
  loading = false;

  filterForm = this.fb.group({
    network: [''],
    status: [''],
  });

  constructor(
    private fb: FormBuilder,
    private queueService: QueueService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    const { network, status } = this.filterForm.getRawValue();
    this.queueService
      .list({
        network: network || undefined,
        status: status || undefined,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: result => {
          this.dataSource.data = result.items;
          this.totalItems = result.totalItems;
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Erro ao carregar a fila de publicação', 'Fechar', { duration: 5000 });
        },
      });
  }

  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  statusClass(status: PublicationStatus): string {
    switch (status) {
      case 'Scheduled':
        return 'status-scheduled';
      case 'Published':
        return 'status-published';
      case 'Failed':
        return 'status-failed';
      case 'ManualPending':
        return 'status-manual-pending';
      default:
        return '';
    }
  }

  retry(item: QueueItem): void {
    this.queueService.retry(item.id).subscribe({
      next: () => {
        this.snackBar.open('Item reenviado para a fila', 'Fechar', { duration: 3000 });
        this.load();
      },
      error: () => this.snackBar.open('Erro ao reprocessar item', 'Fechar', { duration: 5000 }),
    });
  }
}
