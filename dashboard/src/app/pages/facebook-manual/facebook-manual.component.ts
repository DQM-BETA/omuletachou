import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { QueueService, QueueItem } from '../../core/services/queue.service';
import { ProductsService, ProductDetail } from '../../core/services/products.service';

export interface ManualPost {
  queueItem: QueueItem;
  product: ProductDetail | null;
  publishing: boolean;
}

@Component({
  selector: 'app-facebook-manual',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './facebook-manual.component.html',
  styleUrl: './facebook-manual.component.scss',
})
export class FacebookManualComponent implements OnInit {
  posts: ManualPost[] = [];
  loading = true;
  errorMessage: string | null = null;

  constructor(
    private queueService: QueueService,
    private productsService: ProductsService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadPendingPosts();
  }

  loadPendingPosts(): void {
    this.loading = true;
    this.errorMessage = null;

    this.queueService
      .listManualPending({ page: 1, pageSize: 50 })
      .pipe(
        switchMap((result) => {
          const facebookItems = result.items.filter((item) => item.socialNetwork === 'Facebook');

          if (facebookItems.length === 0) {
            return of([] as ManualPost[]);
          }

          const productRequests = facebookItems.map((item) =>
            this.productsService.getById(item.productId).pipe(
              catchError(() => of(null))
            )
          );

          return forkJoin(productRequests).pipe(
            switchMap((products) =>
              of(
                facebookItems.map(
                  (item, index) =>
                    ({
                      queueItem: item,
                      product: products[index],
                      publishing: false,
                    } as ManualPost)
                )
              )
            )
          );
        }),
        catchError(() => {
          this.errorMessage = 'Erro ao carregar posts pendentes.';
          return of([] as ManualPost[]);
        })
      )
      .subscribe((posts) => {
        this.posts = posts;
        this.loading = false;
      });
  }

  copyCaption(caption: string): void {
    navigator.clipboard.writeText(caption).then(
      () => this.snackBar.open('Legenda copiada!', 'Fechar', { duration: 3000 }),
      () => this.snackBar.open('Falha ao copiar legenda.', 'Fechar', { duration: 3000 })
    );
  }

  markAsPublished(post: ManualPost): void {
    post.publishing = true;
    this.queueService.markPublished(post.queueItem.id).subscribe({
      next: () => {
        this.posts = this.posts.filter((p) => p.queueItem.id !== post.queueItem.id);
        this.snackBar.open('Post marcado como publicado!', 'Fechar', { duration: 3000 });
      },
      error: () => {
        post.publishing = false;
        this.snackBar.open('Erro ao marcar post como publicado.', 'Fechar', { duration: 3000 });
      },
    });
  }

  isVideo(url: string | null | undefined): boolean {
    if (!url) return false;
    return /\.(mp4|mov|webm)$/i.test(url);
  }
}
