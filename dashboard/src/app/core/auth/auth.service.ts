import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';

export interface AuthUser {
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'omuletachou_token';
  private tokenSignal = signal<string | null>(this.readStoredToken());

  readonly isAuthenticated = computed(() => !!this.tokenSignal());

  constructor(private http: HttpClient, private router: Router) {}

  login(email: string, password: string): Observable<void> {
    return this.http.post<{ token: string }>('/api/auth/login', { email, password }).pipe(
      tap(res => this.setToken(res.token)),
      map(() => void 0)
    );
  }

  logout(redirectMessage?: string): void {
    this.clearToken();
    this.router.navigate(['/login'], { queryParams: redirectMessage ? { message: redirectMessage } : {} });
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  private setToken(token: string): void {
    sessionStorage.setItem(this.TOKEN_KEY, token);
    this.tokenSignal.set(token);
  }

  private clearToken(): void {
    sessionStorage.removeItem(this.TOKEN_KEY);
    this.tokenSignal.set(null);
  }

  private readStoredToken(): string | null {
    return sessionStorage.getItem(this.TOKEN_KEY);
  }
}
