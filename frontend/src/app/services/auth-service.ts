import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, switchMap } from 'rxjs';
import { UserDetails } from '../models/portfolio';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  constructor(private http: HttpClient) {
  }

  refreshCsrf(): Observable<unknown> {
    return this.http.get('/api/auth/csrf');
  }

  getUser(): Observable<UserDetails> {
    return this.http.get<UserDetails>('/api/auth/me');
  }

  login(email: string, password: string): Observable<UserDetails> {
    return this.refreshCsrf().pipe(
      switchMap(() => this.http.post('/api/auth/login', { email, password })),
      switchMap(() => this.refreshCsrf()),
      switchMap(() => this.getUser())
    );
  }

  register(email: string, password: string): Observable<unknown> {
    return this.refreshCsrf().pipe(
      switchMap(() => this.http.post('/api/auth/register', { email, password }))
    );
  }

  logout(): Observable<unknown> {
    return this.refreshCsrf().pipe(
      switchMap(() => this.http.post('/api/auth/logout', {}))
    );
  }
}
