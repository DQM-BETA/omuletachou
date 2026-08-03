import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

import { FacebookManualComponent } from './facebook-manual.component';
import { QueueService, QueueItem } from '../../core/services/queue.service';
import { ProductsService, ProductDetail } from '../../core/services/products.service';
import { PagedResult } from '../../core/services/paged-result.model';

describe('FacebookManualComponent', () => {
  let component: FacebookManualComponent;
  let fixture: ComponentFixture<FacebookManualComponent>;
  let queueServiceSpy: jasmine.SpyObj<QueueService>;
  let productsServiceSpy: jasmine.SpyObj<ProductsService>;

  const queueItem: QueueItem = {
    id: 'q1',
    productId: 'p1',
    socialNetwork: 'Facebook',
    status: 'ManualPending',
    scheduledAt: '2026-01-01T00:00:00Z',
    publishedAt: null,
    retryCount: 0,
    errorMessage: null,
    createdAt: '2026-01-01T00:00:00Z',
  };

  const nonFacebookItem: QueueItem = {
    ...queueItem,
    id: 'q2',
    socialNetwork: 'Instagram',
  };

  const productDetail: ProductDetail = {
    id: 'p1',
    title: 'Produto X',
    salePrice: 10,
    originalPrice: 20,
    discountPct: 50,
    status: 'Published',
    platform: 'Amazon',
    slug: 'produto-x',
    category: 'cat',
    createdAt: '2026-01-01T00:00:00Z',
    description: 'Descricao original do produto X (nao deve ser usada como legenda)',
    affiliateLink: 'https://x',
    imageUrl: 'https://img/x.jpg',
    mediaUrl: null,
    mediaLocalPath: null,
    updatedAt: '2026-01-01T00:00:00Z',
    ai_caption: 'Legenda de IA gerada para o produto X',
  };

  const productDetailSemLegenda: ProductDetail = {
    ...productDetail,
    ai_caption: null,
  };

  function setup(pagedResult: PagedResult<QueueItem>, product: ProductDetail = productDetail): void {
    queueServiceSpy = jasmine.createSpyObj('QueueService', ['listManualPending', 'markPublished']);
    productsServiceSpy = jasmine.createSpyObj('ProductsService', ['getById']);

    queueServiceSpy.listManualPending.and.returnValue(of(pagedResult));
    productsServiceSpy.getById.and.returnValue(of(product));

    TestBed.configureTestingModule({
      imports: [FacebookManualComponent, NoopAnimationsModule],
      providers: [
        { provide: QueueService, useValue: queueServiceSpy },
        { provide: ProductsService, useValue: productsServiceSpy },
      ],
    });

    fixture = TestBed.createComponent(FacebookManualComponent);
    component = fixture.componentInstance;
  }

  it('should create', () => {
    setup({ items: [], page: 1, pageSize: 50, totalItems: 0, totalPages: 0 });
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('CA-D1: carrega e exibe cards com preview de midia e legenda para posts pendentes do Facebook', () => {
    setup({ items: [queueItem], page: 1, pageSize: 50, totalItems: 1, totalPages: 1 });
    fixture.detectChanges();

    expect(component.loading).toBeFalse();
    expect(component.posts.length).toBe(1);
    expect(component.posts[0].product?.ai_caption).toBe('Legenda de IA gerada para o produto X');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="caption-text"]')?.textContent).toContain(
      'Legenda de IA gerada para o produto X'
    );
    expect(compiled.querySelector('[data-testid="media-image"]')).toBeTruthy();
  });

  it('CA13: usa a legenda de IA (ai_caption) no botao de copiar, nao a descricao original do produto', () => {
    setup({ items: [queueItem], page: 1, pageSize: 50, totalItems: 1, totalPages: 1 });
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const copyButton = compiled.querySelector('[data-testid="copy-caption-button"]') as HTMLButtonElement;
    copyButton.click();

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('Legenda de IA gerada para o produto X');
    expect(navigator.clipboard.writeText).not.toHaveBeenCalledWith(productDetail.description);
  });

  it('CA14: quando ai_caption e null (item legado/nao gerado), exibe fallback explicito sem quebrar a UI', () => {
    setup({ items: [queueItem], page: 1, pageSize: 50, totalItems: 1, totalPages: 1 }, productDetailSemLegenda);
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    fixture.detectChanges();

    expect(component.posts[0].product?.ai_caption).toBeNull();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="caption-text"]')?.textContent).toContain(
      'Legenda não disponível'
    );
    expect(compiled.querySelector('[data-testid="caption-text"]')?.textContent).not.toContain(
      productDetailSemLegenda.description
    );

    const copyButton = compiled.querySelector('[data-testid="copy-caption-button"]') as HTMLButtonElement;
    copyButton.click();
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('');
  });

  it('filtra somente itens ManualPending da rede Facebook', () => {
    setup({
      items: [queueItem, nonFacebookItem],
      page: 1,
      pageSize: 50,
      totalItems: 2,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.posts.length).toBe(1);
    expect(component.posts[0].queueItem.socialNetwork).toBe('Facebook');
  });

  it('exibe mensagem vazia quando nao ha posts pendentes', () => {
    setup({ items: [], page: 1, pageSize: 50, totalItems: 0, totalPages: 0 });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="empty-message"]')).toBeTruthy();
  });

  it('exibe mensagem de erro quando a chamada falha', () => {
    queueServiceSpy = jasmine.createSpyObj('QueueService', ['listManualPending', 'markPublished']);
    productsServiceSpy = jasmine.createSpyObj('ProductsService', ['getById']);
    queueServiceSpy.listManualPending.and.returnValue(throwError(() => new Error('fail')));

    TestBed.configureTestingModule({
      imports: [FacebookManualComponent, NoopAnimationsModule],
      providers: [
        { provide: QueueService, useValue: queueServiceSpy },
        { provide: ProductsService, useValue: productsServiceSpy },
      ],
    });

    fixture = TestBed.createComponent(FacebookManualComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.errorMessage).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="error-message"]')).toBeTruthy();
  });

  it('CA-D2: copia a legenda para a area de transferencia', async () => {
    setup({ items: [queueItem], page: 1, pageSize: 50, totalItems: 1, totalPages: 1 });
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    fixture.detectChanges();

    component.copyCaption('Legenda completa do produto X');

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('Legenda completa do produto X');
  });

  it('CA-D3: marca o post como publicado e remove o card da lista', () => {
    setup({ items: [queueItem], page: 1, pageSize: 50, totalItems: 1, totalPages: 1 });
    queueServiceSpy.markPublished.and.returnValue(of(void 0));
    fixture.detectChanges();

    expect(component.posts.length).toBe(1);

    component.markAsPublished(component.posts[0]);

    expect(queueServiceSpy.markPublished).toHaveBeenCalledWith('q1');
    expect(component.posts.length).toBe(0);
  });

  it('mantem o card quando markAsPublished falha', () => {
    setup({ items: [queueItem], page: 1, pageSize: 50, totalItems: 1, totalPages: 1 });
    queueServiceSpy.markPublished.and.returnValue(throwError(() => new Error('fail')));
    fixture.detectChanges();

    const post = component.posts[0];
    component.markAsPublished(post);

    expect(component.posts.length).toBe(1);
    expect(post.publishing).toBeFalse();
  });
});
