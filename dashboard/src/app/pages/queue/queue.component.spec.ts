import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { QueueComponent } from './queue.component';
import { PagedResult } from '../../core/services/paged-result.model';
import { QueueItem } from '../../core/services/queue.service';

describe('QueueComponent', () => {
  let component: QueueComponent;
  let fixture: ComponentFixture<QueueComponent>;
  let httpMock: HttpTestingController;

  const mockPage: PagedResult<QueueItem> = {
    items: [
      {
        id: 'q1',
        productId: 'p1',
        socialNetwork: 'Telegram',
        status: 'Scheduled',
        scheduledAt: '2026-07-22T10:00:00Z',
        publishedAt: null,
        retryCount: 0,
        errorMessage: null,
        createdAt: '2026-07-21T10:00:00Z',
      },
      {
        id: 'q2',
        productId: 'p2',
        socialNetwork: 'Youtube',
        status: 'Failed',
        scheduledAt: '2026-07-21T10:00:00Z',
        publishedAt: null,
        retryCount: 2,
        errorMessage: 'Timeout ao publicar',
        createdAt: '2026-07-20T10:00:00Z',
      },
      {
        id: 'q3',
        productId: 'p3',
        socialNetwork: 'Instagram',
        status: 'ManualPending',
        scheduledAt: '2026-07-20T10:00:00Z',
        publishedAt: null,
        retryCount: 0,
        errorMessage: null,
        createdAt: '2026-07-19T10:00:00Z',
      },
    ],
    page: 1,
    pageSize: 20,
    totalItems: 3,
    totalPages: 1,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QueueComponent, HttpClientTestingModule, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(QueueComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url === '/api/queue');
    req.flush(mockPage);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('CA-B6 — carrega itens da fila e classifica status por cor', () => {
    expect(component.dataSource.data.length).toBe(3);
    expect(component.statusClass('Scheduled')).toBe('status-scheduled');
    expect(component.statusClass('Published')).toBe('status-published');
    expect(component.statusClass('Failed')).toBe('status-failed');
    expect(component.statusClass('ManualPending')).toBe('status-manual-pending');
  });

  it('CA-B6 — renderiza badge de status na tabela para cada item', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const badges = compiled.querySelectorAll('[data-testid="queue-status-badge"]');
    expect(badges.length).toBe(3);
  });

  it('CA-B7 — retry() chama o serviço e recarrega a fila', () => {
    component.retry(mockPage.items[1]);

    const retryReq = httpMock.expectOne('/api/queue/q2/retry');
    expect(retryReq.request.method).toBe('POST');
    retryReq.flush(null);

    const reloadReq = httpMock.expectOne(r => r.url === '/api/queue');
    reloadReq.flush(mockPage);
  });

  it('CA-B7 — botão Retry só aparece para itens com status Failed', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const retryButtons = compiled.querySelectorAll('[data-testid="retry-button"]');
    expect(retryButtons.length).toBe(1);
  });

  it('CA-B8 — applyFilters() reenvia a requisição com filtro de rede/status', () => {
    component.filterForm.patchValue({ network: 'Telegram', status: 'Scheduled' });
    component.applyFilters();

    const req = httpMock.expectOne(
      r => r.url === '/api/queue' && r.params.get('network') === 'Telegram' && r.params.get('status') === 'Scheduled'
    );
    req.flush({ ...mockPage, items: [mockPage.items[0]] });

    expect(component.dataSource.data.length).toBe(1);
  });

  it('exibe indicador de loading enquanto a requisição está em andamento', () => {
    component.pageIndex = 1;
    component.load();
    expect(component.loading).toBeTrue();

    const req = httpMock.expectOne(r => r.url === '/api/queue');
    req.flush(mockPage);
    expect(component.loading).toBeFalse();
  });
});
