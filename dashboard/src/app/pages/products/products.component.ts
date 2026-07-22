import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ProductsService, ProductListItem, Platform, ProductStatus } from '../../core/services/products.service';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './products.component.html',
  styleUrl: './products.component.scss',
})
export class ProductsComponent implements OnInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  readonly displayedColumns = [
    'platform',
    'title',
    'price',
    'discount',
    'aiScore',
    'status',
    'createdAt',
    'actions',
  ];

  readonly platforms: Platform[] = ['Amazon', 'MercadoLivre', 'Shopee'];
  readonly statuses: ProductStatus[] = ['Pending', 'Queued', 'Published', 'Rejected', 'Processing', 'Error'];

  dataSource = new MatTableDataSource<ProductListItem>([]);
  totalItems = 0;
  pageIndex = 0;
  pageSize = 20;
  loading = false;

  filterForm = this.fb.group({
    platform: [''],
    status: [''],
    createdAtDate: [''],
  });

  constructor(
    private fb: FormBuilder,
    private productsService: ProductsService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.dataSource.filterPredicate = (item, filter) => {
      if (!filter) return true;
      const itemDate = item.createdAt ? item.createdAt.slice(0, 10) : '';
      return itemDate === filter;
    };
    this.load();
  }

  load(): void {
    this.loading = true;
    const { platform, status } = this.filterForm.getRawValue();
    this.productsService
      .list({
        platform: platform || undefined,
        status: status || undefined,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: result => {
          this.dataSource.data = result.items;
          this.totalItems = result.totalItems;
          this.applyDateFilter();
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Erro ao carregar produtos', 'Fechar', { duration: 5000 });
        },
      });
  }

  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  applyDateFilter(): void {
    const date = this.filterForm.getRawValue().createdAtDate;
    this.dataSource.filter = date ? date : '';
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  aiScoreClass(score: number | null | undefined): string {
    if (score === null || score === undefined) return 'ai-score-none';
    if (score >= 8) return 'ai-score-green';
    if (score >= 6) return 'ai-score-yellow';
    return 'ai-score-red';
  }

  approve(product: ProductListItem): void {
    this.productsService.updateStatus(product.id, 'pending').subscribe({
      next: () => {
        this.snackBar.open('Produto aprovado', 'Fechar', { duration: 3000 });
        this.load();
      },
      error: () => this.snackBar.open('Erro ao aprovar produto', 'Fechar', { duration: 5000 }),
    });
  }

  reject(product: ProductListItem): void {
    this.productsService.updateStatus(product.id, 'rejected').subscribe({
      next: () => {
        this.snackBar.open('Produto rejeitado', 'Fechar', { duration: 3000 });
        this.load();
      },
      error: () => this.snackBar.open('Erro ao rejeitar produto', 'Fechar', { duration: 5000 }),
    });
  }
}
