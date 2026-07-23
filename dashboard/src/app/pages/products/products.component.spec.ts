import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ProductsComponent } from './products.component';
import { PagedResult } from '../../core/services/paged-result.model';
import { ProductListItem } from '../../core/services/products.service';

describe('ProductsComponent', () => {
  let component: ProductsComponent;
  let fixture: ComponentFixture<ProductsComponent>;
  let httpMock: HttpTestingController;

  const mockPage: PagedResult<ProductListItem> = {
    items: [
      {
        id: 'p1',
        title: 'Fone Bluetooth',
        salePrice: 99.9,
        originalPrice: 199.9,
        discountPct: 50,
        status: 'Pending',
        platform: 'Amazon',
        slug: 'fone-bluetooth',
        category: 'eletronicos',
        createdAt: '2026-07-20T10:00:00Z',
        ai_score: 9,
        ai_reason: 'Alta demanda e bom desconto',
      },
      {
        id: 'p2',
        title: 'Carregador USB-C',
        salePrice: 29.9,
        originalPrice: 39.9,
        discountPct: 25,
        status: 'Pending',
        platform: 'Shopee',
        slug: 'carregador-usbc',
        category: 'eletronicos',
        createdAt: '2026-07-19T10:00:00Z',
        ai_score: 4,
        ai_reason: 'Baixa margem',
      },
    ],
    page: 1,
    pageSize: 20,
    totalItems: 2,
    totalPages: 1,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductsComponent, HttpClientTestingModule, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url === '/api/products');
    req.flush(mockPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('CA-B1 — carrega os produtos retornados pela API na tabela', () => {
    expect(component.dataSource.data.length).toBe(2);
  });

  it('CA-B2 — aiScoreClass classifica corretamente as faixas de score', () => {
    expect(component.aiScoreClass(9)).toBe('ai-score-green');
    expect(component.aiScoreClass(7)).toBe('ai-score-yellow');
    expect(component.aiScoreClass(3)).toBe('ai-score-red');
    expect(component.aiScoreClass(null)).toBe('ai-score-none');
  });

  it('CA-B1/CA-B2 — renderiza badge de ai_score na tabela', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const badges = compiled.querySelectorAll('[data-testid="ai-score-badge"]');
    expect(badges.length).toBe(2);
    expect(badges[0].textContent?.trim()).toBe('9');
  });

  it('CA-B3 — applyFilters() reenvia a requisição com os filtros selecionados', () => {
    component.filterForm.patchValue({ platform: 'Amazon', status: 'Pending' });
    component.applyFilters();

    const req = httpMock.expectOne(
      r => r.url === '/api/products' && r.params.get('platform') === 'Amazon' && r.params.get('status') === 'Pending'
    );
    req.flush({ ...mockPage, items: [mockPage.items[0]] });

    expect(component.dataSource.data.length).toBe(1);
  });

  it('CA-B3 — filtro de data de coleta filtra a tabela no cliente', () => {
    component.filterForm.patchValue({ createdAtDate: '2026-07-20' });
    component.applyDateFilter();
    fixture.detectChanges();

    expect(component.dataSource.filteredData.length).toBe(1);
    expect(component.dataSource.filteredData[0].id).toBe('p1');
  });

  it('CA-B4 — approve() chama PATCH status=pending e recarrega a lista', () => {
    component.approve(mockPage.items[0]);

    const patchReq = httpMock.expectOne('/api/products/p1/status');
    expect(patchReq.request.method).toBe('PATCH');
    expect(patchReq.request.body).toEqual({ status: 'pending' });
    patchReq.flush(null);

    const reloadReq = httpMock.expectOne(r => r.url === '/api/products');
    reloadReq.flush(mockPage);
  });

  it('CA-B5 — reject() chama PATCH status=rejected e recarrega a lista', () => {
    component.reject(mockPage.items[1]);

    const patchReq = httpMock.expectOne('/api/products/p2/status');
    expect(patchReq.request.method).toBe('PATCH');
    expect(patchReq.request.body).toEqual({ status: 'rejected' });
    patchReq.flush(null);

    const reloadReq = httpMock.expectOne(r => r.url === '/api/products');
    reloadReq.flush(mockPage);
  });

  it('exibe indicador de loading enquanto a requisição está em andamento', () => {
    component.pageIndex = 1;
    component.load();
    expect(component.loading).toBeTrue();

    const req = httpMock.expectOne(r => r.url === '/api/products');
    req.flush(mockPage);
    expect(component.loading).toBeFalse();
  });
});
