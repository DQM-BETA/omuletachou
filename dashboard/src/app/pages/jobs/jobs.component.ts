import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';

import { JobKind, JobsService } from '../../core/services/jobs.service';

interface JobButton {
  kind: JobKind;
  label: string;
  triggering: boolean;
  lastResult: 'success' | 'error' | null;
  lastMessage: string | null;
}

@Component({
  selector: 'app-jobs',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './jobs.component.html',
  styleUrl: './jobs.component.scss',
})
export class JobsComponent {
  readonly jobs: JobButton[] = [
    { kind: 'collector', label: 'Collector (geral)', triggering: false, lastResult: null, lastMessage: null },
    {
      kind: 'collector-amazon',
      label: 'Collector — Amazon',
      triggering: false,
      lastResult: null,
      lastMessage: null,
    },
    {
      kind: 'collector-mercadolivre',
      label: 'Collector — MercadoLivre',
      triggering: false,
      lastResult: null,
      lastMessage: null,
    },
    {
      kind: 'collector-shopee',
      label: 'Collector — Shopee',
      triggering: false,
      lastResult: null,
      lastMessage: null,
    },
    { kind: 'processor', label: 'Processor', triggering: false, lastResult: null, lastMessage: null },
    { kind: 'publisher', label: 'Publisher', triggering: false, lastResult: null, lastMessage: null },
  ];

  constructor(private jobsService: JobsService, private snackBar: MatSnackBar) {}

  trigger(job: JobButton): void {
    job.triggering = true;
    job.lastResult = null;
    job.lastMessage = null;

    this.jobsService.trigger(job.kind).subscribe({
      next: response => {
        job.triggering = false;
        job.lastResult = 'success';
        job.lastMessage =
          response?.count !== undefined
            ? `Disparado com sucesso (${response.count} itens).`
            : 'Disparado com sucesso.';
        this.snackBar.open(`${job.label}: ${job.lastMessage}`, 'Fechar', { duration: 4000 });
      },
      error: () => {
        job.triggering = false;
        job.lastResult = 'error';
        job.lastMessage = 'Falha ao disparar o job.';
        this.snackBar.open(`${job.label}: ${job.lastMessage}`, 'Fechar', { duration: 4000 });
      },
    });
  }
}
