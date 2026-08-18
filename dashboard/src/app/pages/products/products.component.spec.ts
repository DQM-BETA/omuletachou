import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatTooltip } from '@angular/material/tooltip';
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

  it('CA-B6 — tooltip com o motivo do erro aparece no badge de Status (não no de AI Score) quando Status = Error', () => {
    const errorProduct: ProductListItem = {
      id: 'p3',
      title: 'Produto com falha de publicação',
      salePrice: 59.9,
      originalPrice: 79.9,
      discountPct: 25,
      status: 'Error',
      platform: 'Amazon',
      slug: 'produto-falha-publicacao',
      category: 'eletronicos',
      createdAt: '2026-07-18T10:00:00Z',
      ai_score: 9,
      ai_reason: 'Nenhuma rede social habilitada com credenciais válidas para publicar este produto.',
    };

    component.filterForm.patchValue({ status: 'Error' });
    component.applyFilters();

    const req = httpMock.expectOne(r => r.url === '/api/products' && r.params.get('status') === 'Error');
    req.flush({ ...mockPage, items: [errorProduct], totalItems: 1 });
    fixture.detectChanges();

    const statusBadge = fixture.debugElement.query(By.css('[data-testid="status-badge"]'));
    const statusTooltip = statusBadge.injector.get(MatTooltip);
    expect(statusTooltip.disabled).toBeFalse();
    expect(statusTooltip.message).toBe(errorProduct.ai_reason as string);

    const aiScoreBadge = fixture.debugElement.query(By.css('[data-testid="ai-score-badge"]'));
    const aiScoreTooltip = aiScoreBadge.injector.get(MatTooltip);
    expect(aiScoreTooltip.disabled).toBeTrue();
  });

  it('CA-B7 — tooltip de justificativa do AI Score permanece no badge de AI Score quando Status != Error', () => {
    const aiScoreBadge = fixture.debugElement.queryAll(By.css('[data-testid="ai-score-badge"]'))[0];
    const aiScoreTooltip = aiScoreBadge.injector.get(MatTooltip);
    expect(aiScoreTooltip.disabled).toBeFalse();
    expect(aiScoreTooltip.message).toBe(mockPage.items[0].ai_reason as string);

    const statusBadge = fixture.debugElement.queryAll(By.css('[data-testid="status-badge"]'))[0];
    const statusTooltip = statusBadge.injector.get(MatTooltip);
    expect(statusTooltip.disabled).toBeTrue();
  });

  it('T-03/CA-4.2 — buildDestinationsTooltip() monta a string traduzida a partir dos destinos', () => {
    const tooltip = component.buildDestinationsTooltip([
      { destination: 'Site', status: 'Published' },
      { destination: 'Telegram', status: 'Published' },
      { destination: 'Youtube', status: 'NotApplicable' },
      { destination: 'Instagram', status: 'NotApplicable' },
      { destination: 'TikTok', status: 'NotApplicable' },
      { destination: 'Facebook', status: 'Pending' },
    ]);

    expect(tooltip).toBe(
      'Site: Publicado · Telegram: Publicado · Youtube: Não aplicável · Instagram: Não aplicável · TikTok: Não aplicável · Facebook: Pendente'
    );
  });

  it('T-03/CA-4.2 — buildDestinationsTooltip() traduz status Failed para "Erro"', () => {
    const tooltip = component.buildDestinationsTooltip([{ destination: 'Telegram', status: 'Failed' }]);
    expect(tooltip).toBe('Telegram: Erro');
  });

  it('T-03/CA-4.2 — buildDestinationsTooltip() retorna string vazia quando destinations é undefined/vazio', () => {
    expect(component.buildDestinationsTooltip(undefined)).toBe('');
    expect(component.buildDestinationsTooltip([])).toBe('');
  });

  it('T-03/CA-4.2 — badge de Status = Published exibe tooltip com os destinos reais', () => {
    const publishedProduct: ProductListItem = {
      id: 'p4',
      title: 'Produto publicado no site e no Telegram',
      salePrice: 89.9,
      originalPrice: 129.9,
      discountPct: 30,
      status: 'Published',
      platform: 'Amazon',
      slug: 'produto-publicado',
      category: 'eletronicos',
      createdAt: '2026-07-17T10:00:00Z',
      ai_score: 8,
      ai_reason: 'Boa relação custo-benefício',
      destinations: [
        { destination: 'Site', status: 'Published' },
        { destination: 'Telegram', status: 'Published' },
        { destination: 'Youtube', status: 'NotApplicable' },
        { destination: 'Instagram', status: 'NotApplicable' },
        { destination: 'TikTok', status: 'NotApplicable' },
        { destination: 'Facebook', status: 'Pending' },
      ],
    };

    component.filterForm.patchValue({ status: 'Published' });
    component.applyFilters();

    const req = httpMock.expectOne(r => r.url === '/api/products' && r.params.get('status') === 'Published');
    req.flush({ ...mockPage, items: [publishedProduct], totalItems: 1 });
    fixture.detectChanges();

    const statusBadge = fixture.debugElement.query(By.css('[data-testid="status-badge"]'));
    const statusTooltip = statusBadge.injector.get(MatTooltip);
    expect(statusTooltip.disabled).toBeFalse();
    expect(statusTooltip.message).toBe(
      'Site: Publicado · Telegram: Publicado · Youtube: Não aplicável · Instagram: Não aplicável · TikTok: Não aplicável · Facebook: Pendente'
    );
  });

  it('T-03/CA-4.2 — badge de Status != Published continua usando o tooltip de ai_reason (comportamento existente não regride)', () => {
    // mockPage.items[0] tem status Pending, sem destinations — comportamento do CA-B7 preservado.
    const statusBadge = fixture.debugElement.queryAll(By.css('[data-testid="status-badge"]'))[0];
    const statusTooltip = statusBadge.injector.get(MatTooltip);
    expect(statusTooltip.disabled).toBeTrue();
  });

  it('T-03/CA-4.2 — produto Published sem destinations (payload antigo) não quebra e não exibe tooltip', () => {
    const publishedNoDestinations: ProductListItem = {
      id: 'p5',
      title: 'Produto publicado sem campo destinations (payload antigo)',
      salePrice: 49.9,
      originalPrice: 59.9,
      discountPct: 16,
      status: 'Published',
      platform: 'Shopee',
      slug: 'produto-publicado-sem-destinations',
      category: 'eletronicos',
      createdAt: '2026-07-16T10:00:00Z',
      ai_score: 7,
      ai_reason: null,
    };

    component.filterForm.patchValue({ status: 'Published' });
    component.applyFilters();

    const req = httpMock.expectOne(r => r.url === '/api/products' && r.params.get('status') === 'Published');
    req.flush({ ...mockPage, items: [publishedNoDestinations], totalItems: 1 });

    expect(() => fixture.detectChanges()).not.toThrow();

    const statusBadge = fixture.debugElement.query(By.css('[data-testid="status-badge"]'));
    const statusTooltip = statusBadge.injector.get(MatTooltip);
    expect(statusTooltip.disabled).toBeTrue();
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
