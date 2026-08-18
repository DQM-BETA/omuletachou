import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';

import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { TextFieldModule } from '@angular/cdk/text-field';

import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatExpansionModule } from '@angular/material/expansion';

import {
  AffiliateLinkImportItem,
  AffiliateLinkImportSkip,
  ProductListItem,
  ProductsService,
} from '../../core/services/products.service';

@Component({
  selector: 'app-mercadolivre-links',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    TextFieldModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTableModule,
    MatListModule,
    MatChipsModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatExpansionModule,
  ],
  templateUrl: './mercadolivre-links.component.html',
  styleUrl: './mercadolivre-links.component.scss',
})
export class MercadolivreLinksComponent implements OnInit, OnDestroy {
  products: ProductListItem[] = [];
  loading = true;
  errorMessage: string | null = null;

  pastedText = '';
  copied = false;
  importing = false;

  skipped: AffiliateLinkImportSkip[] | null = null;
  panelExpanded = false;
  private productsAtLastImport: ProductListItem[] = [];

  /**
   * Quantidade de produtos pendentes trabalhados por vez ("lote"). A ferramenta oficial do
   * Mercado Livre (Gerador de produtos recomendados) hoje aceita no maximo ~30 URLs por vez,
   * mas esse limite pode mudar — por isso o valor e configuravel pelo operador, com 30 apenas
   * como sugestao inicial (nao hardcoded na logica).
   */
  private _batchSize = 30;

  isMobile = false;
  isTablet = false;
  private breakpointSub?: Subscription;

  readonly displayedColumnsDesktop = ['index', 'title', 'category', 'sourceUrl'];
  readonly displayedColumnsTablet = ['index', 'title', 'sourceUrl'];

  constructor(
    private productsService: ProductsService,
    private snackBar: MatSnackBar,
    private breakpointObserver: BreakpointObserver
  ) {}

  ngOnInit(): void {
    this.load();
    this.breakpointSub = this.breakpointObserver
      .observe([Breakpoints.Handset, Breakpoints.Tablet])
      .subscribe(() => {
        this.isMobile = this.breakpointObserver.isMatched(Breakpoints.Handset);
        this.isTablet = !this.isMobile && this.breakpointObserver.isMatched(Breakpoints.Tablet);
      });
  }

  ngOnDestroy(): void {
    this.breakpointSub?.unsubscribe();
  }

  get displayedColumns(): string[] {
    return this.isTablet ? this.displayedColumnsTablet : this.displayedColumnsDesktop;
  }

  get batchSize(): number {
    return this._batchSize;
  }

  set batchSize(value: number) {
    const parsed = Number(value);
    this._batchSize = Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : 0;
  }

  /** Subconjunto de `products` (no máximo `batchSize` itens) que o operador está trabalhando agora. */
  get displayedProducts(): ProductListItem[] {
    return this.products.slice(0, this.batchSize);
  }

  load(): void {
    this.loading = true;
    this.errorMessage = null;

    this.productsService.listAwaitingAffiliateLink().subscribe({
      next: (result) => {
        this.products = result.items;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Não foi possível carregar os produtos pendentes.';
      },
    });
  }

  get pasteLines(): string[] {
    return this.pastedText
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);
  }

  get pasteCount(): number {
    return this.pasteLines.length;
  }

  get pasteMismatch(): boolean {
    return this.pasteCount !== this.displayedProducts.length;
  }

  get pasteCounterMessage(): string {
    const total = this.displayedProducts.length;
    const count = this.pasteCount;

    if (count === 0) {
      return `0 de ${total} links colados.`;
    }
    if (count === total) {
      return `${count} de ${total} links colados — pronto para importar.`;
    }
    const diff = count - total;
    return diff > 0
      ? `${count} de ${total} links colados — sobram ${diff}.`
      : `${count} de ${total} links colados — faltam ${Math.abs(diff)}.`;
  }

  get pasteCounterIcon(): string {
    if (this.pasteCount === 0) return 'info';
    return this.pasteMismatch ? 'error_outline' : 'check_circle';
  }

  get pasteCounterClass(): string {
    if (this.pasteCount === 0) return 'neutral';
    return this.pasteMismatch ? 'warn' : 'success';
  }

  get canImport(): boolean {
    return (
      !this.importing && this.displayedProducts.length > 0 && this.pasteCount > 0 && !this.pasteMismatch
    );
  }

  copyUrls(): void {
    const text = this.displayedProducts.map((p) => p.sourceUrl ?? '').join('\n');
    navigator.clipboard.writeText(text).then(
      () => {
        this.copied = true;
        setTimeout(() => (this.copied = false), 2000);
      },
      () => this.snackBar.open('Falha ao copiar URLs.', 'Fechar', { duration: 3000 })
    );
  }

  copySingleUrl(url: string | null | undefined): void {
    if (!url) return;
    navigator.clipboard.writeText(url).then(
      () => this.snackBar.open('Link copiado!', 'Fechar', { duration: 2000 }),
      () => this.snackBar.open('Falha ao copiar link.', 'Fechar', { duration: 2000 })
    );
  }

  import(): void {
    if (!this.canImport) return;

    this.importing = true;
    this.skipped = null;
    this.panelExpanded = false;
    this.productsAtLastImport = [...this.displayedProducts];

    const lines = this.pasteLines;
    const items: AffiliateLinkImportItem[] = this.displayedProducts.map((product, index) => ({
      productId: product.id,
      affiliateLink: lines[index],
    }));

    this.productsService.importAffiliateLinks(items).subscribe({
      next: (result) => {
        this.importing = false;
        this.pastedText = '';

        if (result.skipped.length === 0) {
          this.snackBar.open(`${result.imported} produtos importados com sucesso.`, 'Fechar', {
            duration: 4000,
          });
        } else {
          this.skipped = result.skipped;
          const ref = this.snackBar.open(
            `${result.imported} importados, ${result.skipped.length} pulados.`,
            'Ver detalhes',
            { duration: 6000 }
          );
          ref.onAction().subscribe(() => (this.panelExpanded = true));
        }

        this.load();
      },
      error: () => {
        this.importing = false;
        const ref = this.snackBar.open(
          'Não foi possível importar os links. Tente novamente.',
          'Tentar novamente',
          { duration: 6000 }
        );
        ref.onAction().subscribe(() => this.import());
      },
    });
  }

  getSkippedProductTitle(productId: string): string {
    return this.productsAtLastImport.find((p) => p.id === productId)?.title ?? productId;
  }
}
