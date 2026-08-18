import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { OverlayContainer } from '@angular/cdk/overlay';

import { MercadolivreLinksComponent } from './mercadolivre-links.component';
import {
  ImportAffiliateLinksResult,
  ProductListItem,
  ProductsService,
} from '../../core/services/products.service';
import { PagedResult } from '../../core/services/paged-result.model';

/**
 * Nota: MatSnackBarModule (importado pelo proprio componente standalone, em imports[]) declara
 * `providers: [MatSnackBar]` no seu NgModule — isso cria uma instancia de MatSnackBar no
 * environment injector LOCAL do componente, que sombreia qualquer override de MatSnackBar
 * fornecido no nivel do TestBed. Por isso os testes abaixo verificam o snackbar pelo DOM real
 * (via OverlayContainer), em vez de espionar o servico injetado.
 */
describe('MercadolivreLinksComponent', () => {
  let component: MercadolivreLinksComponent;
  let fixture: ComponentFixture<MercadolivreLinksComponent>;
  let productsServiceSpy: jasmine.SpyObj<ProductsService>;
  let breakpointObserverSpy: jasmine.SpyObj<BreakpointObserver>;
  let overlayContainerElement: HTMLElement;

  const product1: ProductListItem = {
    id: 'p1',
    title: 'Air fryer 5L Inox',
    salePrice: 199.9,
    originalPrice: 299.9,
    discountPct: 33,
    status: 'AwaitingAffiliateLink',
    platform: 'MercadoLivre',
    slug: 'air-fryer-5l-inox',
    category: 'Eletrodomesticos',
    createdAt: '2026-08-01T10:00:00Z',
    sourceUrl: 'https://www.mercadolivre.com.br/p/MLB123',
  };

  const product2: ProductListItem = {
    id: 'p2',
    title: 'Fone bluetooth XYZ',
    salePrice: 89.9,
    originalPrice: 129.9,
    discountPct: 30,
    status: 'AwaitingAffiliateLink',
    platform: 'MercadoLivre',
    slug: 'fone-bluetooth-xyz',
    category: 'Eletronicos',
    createdAt: '2026-08-02T10:00:00Z',
    sourceUrl: 'https://www.mercadolivre.com.br/p/MLB456',
  };

  function pagedResult(items: ProductListItem[]): PagedResult<ProductListItem> {
    return { items, page: 1, pageSize: 200, totalItems: items.length, totalPages: 1 };
  }

  function configure(
    productsSpy: jasmine.SpyObj<ProductsService>,
    breakpointSpy: jasmine.SpyObj<BreakpointObserver>
  ): void {
    TestBed.configureTestingModule({
      imports: [MercadolivreLinksComponent, HttpClientTestingModule, RouterTestingModule, NoopAnimationsModule],
      providers: [
        { provide: ProductsService, useValue: productsSpy },
        { provide: BreakpointObserver, useValue: breakpointSpy },
      ],
    });

    fixture = TestBed.createComponent(MercadolivreLinksComponent);
    component = fixture.componentInstance;
    overlayContainerElement = TestBed.inject(OverlayContainer).getContainerElement();
  }

  function setup(
    listResult: PagedResult<ProductListItem> | null = pagedResult([product1, product2]),
    listError = false,
    matched: { handset?: boolean; tablet?: boolean } = {}
  ): void {
    productsServiceSpy = jasmine.createSpyObj('ProductsService', [
      'listAwaitingAffiliateLink',
      'importAffiliateLinks',
    ]);

    if (listError) {
      productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(throwError(() => new Error('fail')));
    } else {
      productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(listResult!));
    }

    breakpointObserverSpy = jasmine.createSpyObj('BreakpointObserver', ['observe', 'isMatched']);
    breakpointObserverSpy.observe.and.returnValue(of({ matches: true, breakpoints: {} }));
    breakpointObserverSpy.isMatched.and.callFake((query: string | string[]) => {
      if (query === Breakpoints.Handset) return !!matched.handset;
      if (query === Breakpoints.Tablet) return !!matched.tablet;
      return false;
    });

    configure(productsServiceSpy, breakpointObserverSpy);
  }

  it('should create', () => {
    setup();
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('carrega e exibe a lista de produtos pendentes', () => {
    setup();
    fixture.detectChanges();

    expect(productsServiceSpy.listAwaitingAffiliateLink).toHaveBeenCalled();
    expect(component.loading).toBeFalse();
    expect(component.products.length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="products-table"]')).toBeTruthy();
    const rows = compiled.querySelectorAll('[data-testid="product-row"]');
    expect(rows.length).toBe(2);
  });

  it('exibe o spinner de carregamento enquanto a lista nao chega', () => {
    productsServiceSpy = jasmine.createSpyObj('ProductsService', [
      'listAwaitingAffiliateLink',
      'importAffiliateLinks',
    ]);
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([])).pipe());

    breakpointObserverSpy = jasmine.createSpyObj('BreakpointObserver', ['observe', 'isMatched']);
    breakpointObserverSpy.observe.and.returnValue(of({ matches: false, breakpoints: {} }));
    breakpointObserverSpy.isMatched.and.returnValue(false);

    configure(productsServiceSpy, breakpointObserverSpy);

    expect(component.loading).toBeTrue();
  });

  it('exibe estado vazio quando nao ha produtos pendentes (e oculta o card de importacao)', () => {
    setup(pagedResult([]));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="empty-message"]')).toBeTruthy();
    expect(compiled.querySelector('[data-testid="copy-urls-button"]')).toBeFalsy();
    expect(compiled.querySelector('[data-testid="links-textarea"]')).toBeFalsy();
  });

  it('exibe estado de erro com botao de tentar novamente quando o GET falha', () => {
    setup(null, true);
    fixture.detectChanges();

    expect(component.errorMessage).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    const retryButton = compiled.querySelector('[data-testid="retry-button"]') as HTMLButtonElement;
    expect(retryButton).toBeTruthy();
    expect(compiled.querySelector('[data-testid="links-textarea"]')).toBeFalsy();

    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([product1, product2])));
    retryButton.click();
    fixture.detectChanges();

    expect(component.errorMessage).toBeNull();
    expect(component.products.length).toBe(2);
  });

  it('copia todas as URLs na mesma ordem e mostra feedback "Copiado!" por 2s', fakeAsync(() => {
    setup();
    fixture.detectChanges();
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());

    const compiled = fixture.nativeElement as HTMLElement;
    const copyButton = compiled.querySelector('[data-testid="copy-urls-button"]') as HTMLButtonElement;
    expect(copyButton.textContent).toContain('Copiar URLs (2)');

    copyButton.click();
    tick();
    fixture.detectChanges();

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      'https://www.mercadolivre.com.br/p/MLB123\nhttps://www.mercadolivre.com.br/p/MLB456'
    );
    expect(component.copied).toBeTrue();

    tick(2000);
    expect(component.copied).toBeFalse();
  }));

  it('bloqueia a importacao quando a contagem de linhas coladas diverge da lista (mismatch)', () => {
    setup();
    fixture.detectChanges();

    component.pastedText = 'https://ml.com/afiliado/1';
    fixture.detectChanges();

    expect(component.pasteMismatch).toBeTrue();
    expect(component.canImport).toBeFalse();

    const compiled = fixture.nativeElement as HTMLElement;
    const importButton = compiled.querySelector('[data-testid="import-button"]') as HTMLButtonElement;
    expect(importButton.disabled).toBeTrue();

    const counter = compiled.querySelector('[data-testid="paste-counter"]') as HTMLElement;
    expect(counter.textContent).toContain('faltam');
    expect(productsServiceSpy.importAffiliateLinks).not.toHaveBeenCalled();
  });

  it('habilita a importacao quando a contagem bate e importa com sucesso total (lista recarrega, textarea limpa)', () => {
    setup();
    fixture.detectChanges();

    const result: ImportAffiliateLinksResult = { imported: 2, skipped: [] };
    productsServiceSpy.importAffiliateLinks.and.returnValue(of(result));
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([])));

    component.pastedText = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    fixture.detectChanges();

    expect(component.canImport).toBeTrue();

    component.import();
    fixture.detectChanges();

    expect(productsServiceSpy.importAffiliateLinks).toHaveBeenCalledWith([
      { productId: 'p1', affiliateLink: 'https://ml.com/afiliado/1' },
      { productId: 'p2', affiliateLink: 'https://ml.com/afiliado/2' },
    ]);
    expect(component.pastedText).toBe('');
    expect(component.importing).toBeFalse();
    expect(component.products.length).toBe(0);
    expect(overlayContainerElement.textContent).toContain('2 produtos importados com sucesso.');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="empty-message"]')).toBeTruthy();
  });

  it('importacao parcial: mostra o painel de pulados com motivo e mantem os pulados na lista apos reload', () => {
    setup();
    fixture.detectChanges();

    const result: ImportAffiliateLinksResult = {
      imported: 1,
      skipped: [{ productId: 'p2', reason: 'Link vazio' }],
    };
    productsServiceSpy.importAffiliateLinks.and.returnValue(of(result));
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([product2])));

    // 2 produtos exibidos -> precisa de 2 linhas coladas para nao bloquear o envio (o backend
    // e quem decide, pelo status/produto, que o item 2 sera pulado por "Link vazio").
    component.pastedText = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    fixture.detectChanges();

    component.import();
    fixture.detectChanges();

    expect(component.skipped).toEqual([{ productId: 'p2', reason: 'Link vazio' }]);
    expect(component.products).toEqual([product2]);
    expect(overlayContainerElement.textContent).toContain('1 importados, 1 pulados.');

    const compiled = fixture.nativeElement as HTMLElement;
    const panel = compiled.querySelector('[data-testid="skipped-panel"]');
    expect(panel).toBeTruthy();
    expect(compiled.querySelector('[data-testid="skipped-item"]')?.textContent).toContain(
      'Fone bluetooth XYZ — Link vazio'
    );

    expect(component.panelExpanded).toBeFalse();
    const detailsButton = Array.from(overlayContainerElement.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Ver detalhes')
    ) as HTMLButtonElement;
    expect(detailsButton).toBeTruthy();
    detailsButton.click();
    fixture.detectChanges();

    expect(component.panelExpanded).toBeTrue();
  });

  it('BUG #195: import que zera a esperar (0 importados, N pulados) mantem o painel de pulados visivel mesmo com a lista vazia', () => {
    setup();
    fixture.detectChanges();

    const result: ImportAffiliateLinksResult = {
      imported: 0,
      skipped: [
        { productId: 'p1', reason: 'Status atual e Pending, esperado AwaitingAffiliateLink' },
        { productId: 'p2', reason: 'Link vazio' },
      ],
    };
    productsServiceSpy.importAffiliateLinks.and.returnValue(of(result));
    // reload esvazia a lista de pendentes (cenario comum: lote inteiro resolvido, por skip ou sucesso)
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([])));

    component.pastedText = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    fixture.detectChanges();

    component.import();
    fixture.detectChanges();

    expect(component.products.length).toBe(0);
    expect(component.skipped?.length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    // painel de skipped precisa continuar no DOM mesmo com o import-card pai sem produtos pendentes
    const panel = compiled.querySelector('[data-testid="skipped-panel"]');
    expect(panel).toBeTruthy();
    const items = compiled.querySelectorAll('[data-testid="skipped-item"]');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Status atual e Pending, esperado AwaitingAffiliateLink');
    expect(items[1].textContent).toContain('Link vazio');
  });

  it('BUG #195: import misto (alguns importados, alguns pulados) que tambem esvazia a lista mantem o painel de pulados visivel', () => {
    setup();
    fixture.detectChanges();

    const result: ImportAffiliateLinksResult = {
      imported: 1,
      skipped: [{ productId: 'p2', reason: 'Status atual e Pending, esperado AwaitingAffiliateLink' }],
    };
    productsServiceSpy.importAffiliateLinks.and.returnValue(of(result));
    // condicao de corrida plausivel: p2 mudou de status entre o load() e o clique em Importar,
    // e o reload nao traz nenhum produto novo -> lista fica vazia mesmo com skip pendente
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([])));

    component.pastedText = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    fixture.detectChanges();

    component.import();
    fixture.detectChanges();

    expect(component.products.length).toBe(0);
    expect(component.skipped).toEqual([
      { productId: 'p2', reason: 'Status atual e Pending, esperado AwaitingAffiliateLink' },
    ]);

    const compiled = fixture.nativeElement as HTMLElement;
    const panel = compiled.querySelector('[data-testid="skipped-panel"]');
    expect(panel).toBeTruthy();
    expect(compiled.querySelector('[data-testid="skipped-item"]')?.textContent).toContain(
      'Fone bluetooth XYZ — Status atual e Pending, esperado AwaitingAffiliateLink'
    );
    // sem produtos pendentes: nao deve exibir textarea/botao de importar (evita reenvio inconsistente)
    expect(compiled.querySelector('[data-testid="links-textarea"]')).toBeFalsy();
  });

  it('BUG #195: import 100% bem-sucedido que esvazia a lista NAO exibe painel de pulados (sem regressao do caminho feliz)', () => {
    setup();
    fixture.detectChanges();

    const result: ImportAffiliateLinksResult = { imported: 2, skipped: [] };
    productsServiceSpy.importAffiliateLinks.and.returnValue(of(result));
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([])));

    component.pastedText = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    fixture.detectChanges();

    component.import();
    fixture.detectChanges();

    expect(component.products.length).toBe(0);
    // caminho feliz (0 pulados): o componente nunca atribui [] a `skipped` (so entra no branch
    // `else` quando skipped.length > 0) — continua null, e o painel nao deve ser exibido
    expect(component.skipped).toBeNull();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="skipped-panel"]')).toBeFalsy();
    expect(compiled.querySelector('[data-testid="empty-message"]')).toBeTruthy();
  });

  it('BUG #195: "Ver detalhes" da snackbar expande o painel de pulados corretamente mesmo com a lista de pendentes vazia', () => {
    setup();
    fixture.detectChanges();

    const result: ImportAffiliateLinksResult = {
      imported: 0,
      skipped: [{ productId: 'p1', reason: 'Link vazio' }, { productId: 'p2', reason: 'Link vazio' }],
    };
    productsServiceSpy.importAffiliateLinks.and.returnValue(of(result));
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([])));

    component.pastedText = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    fixture.detectChanges();

    component.import();
    fixture.detectChanges();

    expect(component.panelExpanded).toBeFalse();
    const detailsButton = Array.from(overlayContainerElement.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Ver detalhes')
    ) as HTMLButtonElement;
    expect(detailsButton).toBeTruthy();
    detailsButton.click();
    fixture.detectChanges();

    expect(component.panelExpanded).toBeTrue();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="skipped-panel"]')).toBeTruthy();
  });

  it('erro na importacao: exibe snackbar de erro e preserva o conteudo da textarea', () => {
    setup();
    fixture.detectChanges();

    productsServiceSpy.importAffiliateLinks.and.returnValue(throwError(() => new Error('network fail')));

    const pasted = 'https://ml.com/afiliado/1\nhttps://ml.com/afiliado/2';
    component.pastedText = pasted;
    fixture.detectChanges();

    component.import();
    fixture.detectChanges();

    expect(component.importing).toBeFalse();
    expect(component.pastedText).toBe(pasted);
    expect(overlayContainerElement.textContent).toContain(
      'Não foi possível importar os links. Tente novamente.'
    );

    // controle e liberdade do usuario: acionar "Tentar novamente" reenvia sem perder o texto colado
    productsServiceSpy.importAffiliateLinks.and.returnValue(of({ imported: 2, skipped: [] }));
    const retryButton = Array.from(overlayContainerElement.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Tentar novamente')
    ) as HTMLButtonElement;
    expect(retryButton).toBeTruthy();
    retryButton.click();
    fixture.detectChanges();

    expect(productsServiceSpy.importAffiliateLinks).toHaveBeenCalledTimes(2);
  });

  it('desabilita a textarea e o botao Importar durante o carregamento inicial', () => {
    productsServiceSpy = jasmine.createSpyObj('ProductsService', [
      'listAwaitingAffiliateLink',
      'importAffiliateLinks',
    ]);
    productsServiceSpy.listAwaitingAffiliateLink.and.returnValue(of(pagedResult([product1])));
    breakpointObserverSpy = jasmine.createSpyObj('BreakpointObserver', ['observe', 'isMatched']);
    breakpointObserverSpy.observe.and.returnValue(of({ matches: false, breakpoints: {} }));
    breakpointObserverSpy.isMatched.and.returnValue(false);

    configure(productsServiceSpy, breakpointObserverSpy);

    expect(component.loading).toBeTrue();
    expect(component.canImport).toBeFalse();
  });

  it('breakpoint mobile (Handset): exibe mat-list em vez de mat-table e botoes em largura total', () => {
    setup(pagedResult([product1, product2]), false, { handset: true });
    fixture.detectChanges();

    expect(component.isMobile).toBeTrue();
    expect(component.isTablet).toBeFalse();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[data-testid="products-list-mobile"]')).toBeTruthy();
    expect(compiled.querySelector('[data-testid="products-table"]')).toBeFalsy();

    const copyButton = compiled.querySelector('[data-testid="copy-urls-button"]') as HTMLButtonElement;
    expect(copyButton.classList.contains('full-width-mobile')).toBeTrue();
  });

  it('breakpoint tablet: oculta a coluna Categoria na tabela', () => {
    setup(pagedResult([product1, product2]), false, { tablet: true });
    fixture.detectChanges();

    expect(component.isTablet).toBeTrue();
    expect(component.isMobile).toBeFalse();
    expect(component.displayedColumns).toEqual(['index', 'title', 'sourceUrl']);
    expect(component.displayedColumns).not.toContain('category');
  });

  it('breakpoint desktop: exibe todas as colunas, incluindo Categoria', () => {
    setup(pagedResult([product1, product2]), false, {});
    fixture.detectChanges();

    expect(component.isMobile).toBeFalse();
    expect(component.isTablet).toBeFalse();
    expect(component.displayedColumns).toEqual(['index', 'title', 'category', 'sourceUrl']);
  });
});
