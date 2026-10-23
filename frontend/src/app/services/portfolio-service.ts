import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Dashboard, Holding, Investment, PortfolioDetails, Transaction } from '../models/portfolio';

@Injectable({
  providedIn: 'root'
})
export class PortfolioService {
  constructor(private http: HttpClient) {
  }

  getDashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>('/api/dashboard');
  }

  getPortfolios(accountId: number): Observable<PortfolioDetails[]> {
    return this.http.get<PortfolioDetails[]>(`/api/accounts/${accountId}/portfolios`);
  }

  createPortfolio(accountId: number, name: string): Observable<PortfolioDetails> {
    return this.http.post<PortfolioDetails>(`/api/accounts/${accountId}/portfolios`, { name });
  }

  renamePortfolio(accountId: number, id: number, name: string): Observable<PortfolioDetails> {
    return this.http.put<PortfolioDetails>(`/api/accounts/${accountId}/portfolios/${id}`, { name });
  }

  getInvestments(): Observable<Investment[]> {
    return this.http.get<Investment[]>('/api/investments');
  }

  createInvestment(symbol: string, name: string, price: number): Observable<Investment> {
    return this.http.post<Investment>('/api/investments', { symbol, name, price });
  }

  updateInvestment(id: number, price: number): Observable<Investment> {
    return this.http.put<Investment>(`/api/investments/${id}`, { price });
  }

  getHoldings(accountId: number, portfolioId: number): Observable<Holding[]> {
    return this.http.get<Holding[]>(`/api/accounts/${accountId}/portfolios/${portfolioId}/holdings`);
  }

  recordCash(accountId: number, type: string, amount: number): Observable<Transaction> {
    return this.http.post<Transaction>(`/api/accounts/${accountId}/cash-transactions`, { type, amount });
  }

  recordTrade(accountId: number, portfolioId: number, investmentId: number, type: string, quantity: number): Observable<Transaction> {
    return this.http.post<Transaction>(`/api/accounts/${accountId}/portfolios/${portfolioId}/trades`, { investmentId, type, quantity });
  }

  getHistory(accountId: number): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`/api/accounts/${accountId}/history`);
  }
}
