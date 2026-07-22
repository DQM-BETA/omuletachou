import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { FormsModule } from '@angular/forms';

import { SettingsComponent } from './settings.component';
import { Setting } from '../../core/services/settings.service';

describe('SettingsComponent', () => {
  let component: SettingsComponent;
  let fixture: ComponentFixture<SettingsComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsComponent, HttpClientTestingModule, NoopAnimationsModule, MatSnackBarModule, FormsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(SettingsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function loadSettings(settings: Setting[]): void {
    fixture.detectChanges(); // dispara ngOnInit -> getAll()
    const req = httpMock.expectOne('/api/settings');
    req.flush(settings);
    fixture.detectChanges();
  }

  it('should create', () => {
    loadSettings([]);
    expect(component).toBeTruthy();
  });

  it('exibe erro de carregamento e permite tentar novamente se GET /api/settings falhar', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne('/api/settings');
    req.flush('erro interno', { status: 500, statusText: 'Internal Server Error' });
    fixture.detectChanges();

    expect(component.loadError).toBeTrue();
    expect(component.loading).toBeFalse();
  });

  it('CA-C1 — campo sensível carrega vazio, nunca com o valor mascarado ou real, com placeholder informativo', () => {
    loadSettings([{ key: 'telegram.bot_token', value: '****************a1b2' }]);

    const section = component.sections.find(s => s.title === 'Telegram')!;
    const field = section.fields[0];

    expect(field.sensitive).toBeTrue();
    expect(field.inputValue).toBe('');
    expect(field.maskedValue).toBe('****************a1b2');
  });

  it('CA-C1 — placeholder do campo sensível reflete o valor mascarado retornado pela API', () => {
    loadSettings([{ key: 'telegram.bot_token', value: '****************a1b2' }]);
    fixture.detectChanges();

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).not.toContain('valor mascarado errado');
    const input: HTMLInputElement | null = compiled.querySelector('input[name="telegram.bot_token"]');
    expect(input).toBeTruthy();
    expect(input!.placeholder).toBe('Valor atual: ****************a1b2 — digite para substituir');
    expect(input!.value).toBe('');
  });

  it('chave sensível ainda não configurada (value null na API real) exibe placeholder amigável, não "Valor atual: null"', () => {
    loadSettings([{ key: 'amazon.secret_key', value: null }]);

    const section = component.sections.find(s => s.title === 'Amazon')!;
    const field = section.fields[0];
    expect(field.inputValue).toBe('');
    expect(field.maskedValue).toBe('');
    expect(component.placeholderFor(field)).toBe('Nenhum valor configurado — digite para definir');
  });

  it('campo não sensível carrega populado com o valor real', () => {
    loadSettings([{ key: 'claude.min_score', value: '6' }]);

    const section = component.sections.find(s => s.title === 'Claude AI')!;
    expect(section.fields[0].sensitive).toBeFalse();
    expect(section.fields[0].inputValue).toBe('6');
  });

  it('CA-C2 — campo sensível deixado em branco não dispara PUT ao salvar a seção', () => {
    loadSettings([{ key: 'telegram.bot_token', value: '****************a1b2' }]);

    const section = component.sections.find(s => s.title === 'Telegram')!;
    component.save(section);

    httpMock.expectNone('/api/settings/telegram.bot_token');
    expect(section.messageType).toBe('success');
  });

  it('CA-C3 — campo sensível preenchido dispara PUT com o valor completo digitado', () => {
    loadSettings([{ key: 'telegram.bot_token', value: '****************a1b2' }]);

    const section = component.sections.find(s => s.title === 'Telegram')!;
    section.fields[0].inputValue = 'token-novo-completo';
    component.save(section);

    const req = httpMock.expectOne('/api/settings/telegram.bot_token');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ value: 'token-novo-completo' });
    req.flush({ key: 'telegram.bot_token', value: 'token-novo-completo' });

    expect(section.messageType).toBe('success');
  });

  it('CA-C4 — toggle show/hide alterna o campo sensível entre password e text', () => {
    loadSettings([{ key: 'telegram.bot_token', value: '****************a1b2' }]);

    const field = component.sections[0].fields[0];
    expect(field.showPassword).toBeFalse();

    component.togglePasswordVisibility(field);
    expect(field.showPassword).toBeTrue();

    fixture.detectChanges();
    const compiled: HTMLElement = fixture.nativeElement;
    const input: HTMLInputElement = compiled.querySelector('input[name="telegram.bot_token"]')!;
    expect(input.type).toBe('text');

    component.togglePasswordVisibility(field);
    fixture.detectChanges();
    expect(input.type).toBe('password');
  });

  it('CA-C5 — salvar uma seção envia PUT só dos campos alterados daquela seção, sem afetar as demais', () => {
    loadSettings([
      { key: 'telegram.bot_token', value: '****************a1b2' },
      { key: 'claude.min_score', value: '6' },
    ]);

    const telegramSection = component.sections.find(s => s.title === 'Telegram')!;
    const claudeSection = component.sections.find(s => s.title === 'Claude AI')!;

    telegramSection.fields[0].inputValue = 'token-novo';
    claudeSection.fields[0].inputValue = '7';

    component.save(telegramSection);

    const req = httpMock.expectOne('/api/settings/telegram.bot_token');
    expect(req.request.body).toEqual({ value: 'token-novo' });
    req.flush({});
    httpMock.expectNone('/api/settings/claude.min_score');
    expect(claudeSection.fields[0].inputValue).toBe('7');
  });

  it('CA-C6 — não exibe nenhum botão ou referência a "Testar conexão"', () => {
    loadSettings([{ key: 'telegram.bot_token', value: '****************a1b2' }]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).not.toContain('Testar conexão');
    expect(compiled.textContent?.toLowerCase()).not.toContain('testar conexão');
  });

  it('agrupa networks.*.enabled na seção "Redes habilitadas" como toggle booleano', () => {
    loadSettings([{ key: 'networks.telegram.enabled', value: 'true' }]);

    const section = component.sections.find(s => s.title === 'Redes habilitadas')!;
    expect(section.fields[0].isToggle).toBeTrue();
    expect(section.fields[0].sensitive).toBeFalse();
    expect(section.fields[0].toggleValue).toBeTrue();
    expect(section.fields[0].label).toBe('Telegram');
  });

  it('toggle de rede habilitada é sempre enviado ao salvar a seção (nunca fica "em branco")', () => {
    loadSettings([{ key: 'networks.telegram.enabled', value: 'false' }]);

    const section = component.sections.find(s => s.title === 'Redes habilitadas')!;
    section.fields[0].toggleValue = true;
    component.save(section);

    const req = httpMock.expectOne('/api/settings/networks.telegram.enabled');
    expect(req.request.body).toEqual({ value: 'true' });
    req.flush({});
  });

  it('chave não mapeada (hangfire.dashboard_password) cai na seção "Avançado" sem travar o build', () => {
    loadSettings([{ key: 'hangfire.dashboard_password', value: '****xyz' }]);

    const section = component.sections.find(s => s.title === 'Avançado')!;
    expect(section).toBeTruthy();
    expect(section.fields[0].sensitive).toBeTrue();
  });

  it('erro em uma chave da seção não bloqueia o salvamento das demais chaves da mesma seção', () => {
    loadSettings([
      { key: 'amazon.access_key', value: '****aaaa' },
      { key: 'amazon.secret_key', value: '****bbbb' },
    ]);

    const section = component.sections.find(s => s.title === 'Amazon')!;
    section.fields[0].inputValue = 'nova-access-key';
    section.fields[1].inputValue = 'nova-secret-key';

    component.save(section);

    const reqs = httpMock.match(req => req.url.startsWith('/api/settings/amazon.'));
    expect(reqs.length).toBe(2);
    reqs.find(r => r.request.url.endsWith('amazon.access_key'))!.flush('erro', {
      status: 500,
      statusText: 'Internal Server Error',
    });
    reqs.find(r => r.request.url.endsWith('amazon.secret_key'))!.flush({});

    expect(section.saving).toBeFalse();
    expect(section.messageType).toBe('error');
    expect(section.message).toContain('amazon.access_key');
  });
});
