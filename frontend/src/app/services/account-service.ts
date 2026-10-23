import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FinancialAccount } from '../models/financial-account';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private readonly apiUrl: string = '/api/accounts';

  constructor(private http: HttpClient) {
  }

  getAccounts(): Observable<FinancialAccount[]> {
    return this.http.get<FinancialAccount[]>(this.apiUrl);
  }

  createAccount(name: string): Observable<FinancialAccount> {
    return this.http.post<FinancialAccount>(this.apiUrl, { name });
  }

  renameAccount(id: number, name: string): Observable<FinancialAccount> {
    return this.http.put<FinancialAccount>(`${this.apiUrl}/${id}`, { name });
  }
}
