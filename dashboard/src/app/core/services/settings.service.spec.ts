import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import { Setting, SettingsService } from './settings.service';

describe('SettingsService', () => {
  let service: SettingsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [SettingsService],
    });
    service = TestBed.inject(SettingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('deve buscar todas as settings via GET /api/settings', () => {
    const mockSettings: Setting[] = [
      { key: 'telegram.bot_token', value: '****************a1b2' },
      { key: 'claude.min_score', value: '6' },
    ];

    service.getAll().subscribe(result => {
      expect(result).toEqual(mockSettings);
    });

    const req = httpMock.expectOne('/api/settings');
    expect(req.request.method).toBe('GET');
    req.flush(mockSettings);
  });

  it('deve atualizar uma chave individual via PUT /api/settings/{key}', () => {
    const updated: Setting = { key: 'telegram.bot_token', value: 'novo-valor' };

    service.updateOne('telegram.bot_token', 'novo-valor').subscribe(result => {
      expect(result).toEqual(updated);
    });

    const req = httpMock.expectOne('/api/settings/telegram.bot_token');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ value: 'novo-valor' });
    req.flush(updated);
  });

  it('deve propagar erro do PUT sem quebrar o observable (tratado pelo componente)', () => {
    let errorReceived: unknown = null;

    service.updateOne('telegram.bot_token', 'novo-valor').subscribe({
      next: () => fail('não deveria emitir sucesso'),
      error: err => (errorReceived = err),
    });

    const req = httpMock.expectOne('/api/settings/telegram.bot_token');
    req.flush('erro interno', { status: 500, statusText: 'Internal Server Error' });

    expect(errorReceived).toBeTruthy();
  });
});
