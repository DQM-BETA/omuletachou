import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Setting {
  key: string;
  // valor mascarado (****************a1b2) para chaves sensíveis, valor real para não-sensíveis,
  // ou null quando a chave ainda não foi configurada no backend.
  value: string | null;
}

@Injectable({ providedIn: 'root' })
export class SettingsService {
  constructor(private http: HttpClient) {}

  getAll(): Observable<Setting[]> {
    return this.http.get<Setting[]>('/api/settings');
  }

  updateOne(key: string, value: string): Observable<Setting> {
    return this.http.put<Setting>(`/api/settings/${key}`, { value });
  }
}
