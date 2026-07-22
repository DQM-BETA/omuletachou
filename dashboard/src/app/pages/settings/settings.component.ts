import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, forkJoin, map, of } from 'rxjs';

import { Setting, SettingsService } from '../../core/services/settings.service';

const SENSITIVE_KEY_PATTERN = /(_key|_secret|_token|_password)$/i;

interface SettingField {
  key: string;
  label: string;
  sensitive: boolean;
  isToggle: boolean;
  inputValue: string;
  toggleValue: boolean;
  maskedValue: string;
  showPassword: boolean;
}

interface SettingSection {
  title: string;
  fields: SettingField[];
  saving: boolean;
  message: string | null;
  messageType: 'success' | 'error' | null;
}

const SECTION_ORDER = [
  'Amazon',
  'MercadoLivre',
  'Shopee',
  'Telegram',
  'YouTube',
  'Instagram',
  'TikTok',
  'Claude AI',
  'Agendamentos',
  'Redes habilitadas',
  'Avançado',
];

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent implements OnInit {
  sections: SettingSection[] = [];
  loading = true;
  loadError = false;

  constructor(private settingsService: SettingsService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.loadError = false;
    this.settingsService.getAll().subscribe({
      next: settings => {
        this.sections = this.groupBySection(settings);
        this.loading = false;
      },
      error: () => {
        this.loadError = true;
        this.loading = false;
      },
    });
  }

  togglePasswordVisibility(field: SettingField): void {
    field.showPassword = !field.showPassword;
  }

  placeholderFor(field: SettingField): string {
    return field.maskedValue
      ? `Valor atual: ${field.maskedValue} — digite para substituir`
      : 'Nenhum valor configurado — digite para definir';
  }

  save(section: SettingSection): void {
    section.saving = true;
    section.message = null;
    section.messageType = null;

    const updates = section.fields
      .filter(field => !(field.sensitive && field.inputValue === ''))
      .map(field => {
        const value = field.isToggle ? String(field.toggleValue) : field.inputValue;
        return this.settingsService.updateOne(field.key, value).pipe(
          map(() => ({ key: field.key, ok: true as const, field })),
          catchError(() => of({ key: field.key, ok: false as const, field }))
        );
      });

    if (updates.length === 0) {
      section.saving = false;
      section.message = 'Nenhum campo alterado nesta seção.';
      section.messageType = 'success';
      return;
    }

    forkJoin(updates).subscribe(results => {
      section.saving = false;
      const failed = results.filter(result => !result.ok);

      results
        .filter(result => result.ok && result.field.sensitive)
        .forEach(result => (result.field.inputValue = ''));

      if (failed.length === 0) {
        section.message = 'Configurações salvas com sucesso.';
        section.messageType = 'success';
      } else {
        section.message = `Falha ao salvar: ${failed.map(result => result.key).join(', ')}`;
        section.messageType = 'error';
      }
      this.snackBar.open(section.message, 'Fechar', { duration: 4000 });
    });
  }

  private groupBySection(settings: Setting[]): SettingSection[] {
    const bySection = new Map<string, SettingField[]>();
    for (const setting of settings) {
      const title = this.sectionFor(setting.key);
      const field = this.buildField(setting);
      if (!bySection.has(title)) {
        bySection.set(title, []);
      }
      bySection.get(title)!.push(field);
    }

    return SECTION_ORDER.filter(title => bySection.has(title)).map(title => ({
      title,
      fields: bySection.get(title)!,
      saving: false,
      message: null,
      messageType: null,
    }));
  }

  private buildField(setting: Setting): SettingField {
    const sensitive = this.isSensitive(setting.key);
    const isToggle = this.isToggle(setting.key);
    const rawValue = setting.value ?? '';
    return {
      key: setting.key,
      label: this.labelFor(setting.key),
      sensitive,
      isToggle,
      inputValue: sensitive || isToggle ? '' : rawValue,
      toggleValue: isToggle ? rawValue === 'true' : false,
      maskedValue: rawValue,
      showPassword: false,
    };
  }

  private isSensitive(key: string): boolean {
    return SENSITIVE_KEY_PATTERN.test(key);
  }

  private isToggle(key: string): boolean {
    return key.startsWith('networks.') && key.endsWith('.enabled');
  }

  private labelFor(key: string): string {
    if (this.isToggle(key)) {
      const network = key.split('.')[1] ?? key;
      return this.capitalize(network);
    }
    const lastSegment = key.split('.').pop() ?? key;
    return lastSegment
      .split('_')
      .map(word => this.capitalize(word))
      .join(' ');
  }

  private capitalize(word: string): string {
    return word.length === 0 ? word : word[0].toUpperCase() + word.slice(1);
  }

  private sectionFor(key: string): string {
    if (this.isToggle(key)) {
      return 'Redes habilitadas';
    }
    const prefix = key.split('.')[0];
    switch (prefix) {
      case 'amazon':
        return 'Amazon';
      case 'mercadolivre':
        return 'MercadoLivre';
      case 'shopee':
        return 'Shopee';
      case 'telegram':
        return 'Telegram';
      case 'youtube':
        return 'YouTube';
      case 'instagram':
        return 'Instagram';
      case 'tiktok':
        return 'TikTok';
      case 'claude':
        return 'Claude AI';
      case 'schedule':
      case 'publish':
        return 'Agendamentos';
      default:
        return 'Avançado';
    }
  }
}
