import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';

import {
  JobExecutionStatus,
  JobKind,
  JobLastExecutionDto,
  JobsService,
} from '../../core/services/jobs.service';

interface JobButton {
  kind: JobKind;
  label: string;
  triggering: boolean;
  lastResult: 'success' | 'error' | null;
  lastMessage: string | null;
  lastExecutionStatus: JobExecutionStatus;
  lastExecutionStartedAt: string | null;
  lastExecutionFinishedAt: string | null;
  lastExecutionError: string | null;
}

@Component({
  selector: 'app-jobs',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './jobs.component.html',
  styleUrl: './jobs.component.scss',
})
export class JobsComponent implements OnInit {
  readonly jobs: JobButton[] = [
    {
      kind: 'collector',
      label: 'Collector (geral)',
      triggering: false,
      lastResult: null,
      lastMessage: null,
      lastExecutionStatus: null,
      lastExecutionStartedAt: null,
      lastExecutionFinishedAt: null,
      lastExecutionError: null,
    },
    {
      kind: 'collector-amazon',
      label: 'Collector — Amazon',
      triggering: false,
      lastResult: null,
      lastMessage: null,
      lastExecutionStatus: null,
      lastExecutionStartedAt: null,
      lastExecutionFinishedAt: null,
      lastExecutionError: null,
    },
    {
      kind: 'collector-mercadolivre',
      label: 'Collector — MercadoLivre',
      triggering: false,
      lastResult: null,
      lastMessage: null,
      lastExecutionStatus: null,
      lastExecutionStartedAt: null,
      lastExecutionFinishedAt: null,
      lastExecutionError: null,
    },
    {
      kind: 'collector-shopee',
      label: 'Collector — Shopee',
      triggering: false,
      lastResult: null,
      lastMessage: null,
      lastExecutionStatus: null,
      lastExecutionStartedAt: null,
      lastExecutionFinishedAt: null,
      lastExecutionError: null,
    },
    {
      kind: 'processor',
      label: 'Processor',
      triggering: false,
      lastResult: null,
      lastMessage: null,
      lastExecutionStatus: null,
      lastExecutionStartedAt: null,
      lastExecutionFinishedAt: null,
      lastExecutionError: null,
    },
    {
      kind: 'publisher',
      label: 'Publisher',
      triggering: false,
      lastResult: null,
      lastMessage: null,
      lastExecutionStatus: null,
      lastExecutionStartedAt: null,
      lastExecutionFinishedAt: null,
      lastExecutionError: null,
    },
  ];

  constructor(private jobsService: JobsService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.loadLastExecutions();
  }

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
        this.loadLastExecutions();
      },
      error: () => {
        job.triggering = false;
        job.lastResult = 'error';
        job.lastMessage = 'Falha ao disparar o job.';
        this.snackBar.open(`${job.label}: ${job.lastMessage}`, 'Fechar', { duration: 4000 });
        this.loadLastExecutions();
      },
    });
  }

  private loadLastExecutions(): void {
    this.jobsService.getLastExecutions().subscribe(executions => this.mergeLastExecutions(executions));
  }

  private mergeLastExecutions(executions: JobLastExecutionDto[]): void {
    executions.forEach(execution => {
      const job = this.jobs.find(j => j.kind === execution.jobName);
      if (!job) {
        return;
      }
      job.lastExecutionStatus = execution.status;
      job.lastExecutionStartedAt = execution.startedAt;
      job.lastExecutionFinishedAt = execution.finishedAt;
      job.lastExecutionError = execution.errorMessage;
    });
  }
}
