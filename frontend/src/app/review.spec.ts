import { afterEach, describe, expect, it, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { Accounts } from './accounts/accounts';
import { Workspace } from './workspace/workspace';
import { Catalog } from './catalog/catalog';
import { App } from './app';
import { AccountService } from './services/account-service';
import { PortfolioService } from './services/portfolio-service';
import { AuthService } from './services/auth-service';
import { Dashboard, Holding, Investment, Transaction } from './models/portfolio';

const account = { id: 1, name: 'Savings', cashBalance: 100 };
const summary: Dashboard = { cashBalance: 100, investmentValue: 0, totalValue: 100, accountCount: 1, portfolioCount: 0 };
const failure = new HttpErrorResponse({ status: 503, error: { detail: 'Server temporarily unavailable.' } });

function holding(symbol: string): Holding {
  return { investmentId: 1, symbol, quantity: 1,
    currentPrice: 10, marketValue: 10 };
}

function transaction(id: number): Transaction {
  return { id, accountId: 1, portfolioId: null, symbol: null, type: 'Deposit',
    quantity: null, unitPrice: null, grossAmount: 100, recordedAt: '2026-10-21T12:00:00Z' };
}

describe('Review regressions: asynchronous financial views', () => {
  it('keeps newer holdings when an older response or error arrives later', () => {
    const older = new Subject<Holding[]>();
    const newer = new Subject<Holding[]>();
    const service = { getHoldings: vi.fn().mockReturnValueOnce(older).mockReturnValueOnce(newer) };
    const component = new Workspace(service as unknown as PortfolioService);
    component.account = account;
    component.portfolioId = 1;
    component.loadHoldings();
    component.portfolioId = 2;
    component.loadHoldings();
    newer.next([holding('NEW')]);
    older.next([holding('OLD')]);
    older.error(failure);
    expect(component.holdings()[0].symbol).toBe('NEW');
    expect(component.holdingsError()).toBe('');
    expect(component.holdingsLoading()).toBe(false);
  });

  it('invalidates an outstanding holdings request when selection is cleared', () => {
    const response = new Subject<Holding[]>();
    const component = new Workspace({ getHoldings: () => response } as unknown as PortfolioService);
    component.account = account;
    component.portfolioId = 1;
    component.loadHoldings();
    component.portfolioId = null;
    component.loadHoldings();
    response.next([holding('OLD')]);
    expect(component.holdings()).toEqual([]);
    expect(component.holdingsLoading()).toBe(false);
  });

  it('does not replace current history with a delayed older request', () => {
    const older = new Subject<Transaction[]>();
    const newer = new Subject<Transaction[]>();
    const service = { getHistory: vi.fn().mockReturnValueOnce(older).mockReturnValueOnce(newer) };
    const component = new Workspace(service as unknown as PortfolioService);
    component.account = account;
    component.loadHistory();
    component.loadHistory();
    newer.next([transaction(2)]);
    older.next([transaction(1)]);
    older.error(failure);
    expect(component.history()[0].id).toBe(2);
    expect(component.historyError()).toBe('');
  });

  it('clears old results when the new history request fails', () => {
    const component = new Workspace({ getHistory: () => throwError(() => failure) } as unknown as PortfolioService);
    component.account = account;
    component.history.set([transaction(1)]);
    component.loadHistory();
    expect(component.history()).toEqual([]);
    expect(component.historyError()).toContain('Server temporarily unavailable');
  });

  it('keeps account-list success independent from a dashboard failure', () => {
    const accounts = { getAccounts: () => of([account]) };
    const portfolio = { getDashboard: () => throwError(() => failure) };
    const component = new Accounts(accounts as unknown as AccountService, portfolio as unknown as PortfolioService);
    component.loadAccounts();
    expect(component.accounts()).toHaveLength(1);
    expect(component.loadError()).toBe('');
    expect(component.dashboardError()).toContain('Server temporarily unavailable');
    expect(component.dashboardLoading()).toBe(false);
  });

  it('ignores an older summary so balances cannot move backward on screen', () => {
    const older = new Subject<Dashboard>();
    const newer = new Subject<Dashboard>();
    const portfolio = { getDashboard: vi.fn().mockReturnValueOnce(older).mockReturnValueOnce(newer) };
    const component = new Accounts({} as AccountService, portfolio as unknown as PortfolioService);
    component.refreshDashboard();
    component.refreshDashboard();
    newer.next({ ...summary, cashBalance: 200, totalValue: 200 });
    older.next(summary);
    expect(component.dashboard().cashBalance).toBe(200);
  });

  it('preserves the newest account list when responses arrive out of order', () => {
    const older = new Subject<typeof account[]>();
    const newer = new Subject<typeof account[]>();
    const accounts = { getAccounts: vi.fn().mockReturnValueOnce(older).mockReturnValueOnce(newer) };
    const component = new Accounts(accounts as unknown as AccountService, { getDashboard: () => of(summary) } as unknown as PortfolioService);
    component.loadAccounts();
    component.loadAccounts();
    newer.next([{ ...account, name: 'Current name' }]);
    older.next([account]);
    expect(component.accounts()[0].name).toBe('Current name');
  });

  it('keeps the saved account name when an older list response arrives afterward', () => {
    const saved = new Subject<typeof account>();
    const older = new Subject<typeof account[]>();
    const newer = new Subject<typeof account[]>();
    const accounts = {
      renameAccount: () => saved,
      getAccounts: vi.fn().mockReturnValueOnce(older).mockReturnValueOnce(newer)
    };
    const component = new Accounts(accounts as unknown as AccountService,
      { getDashboard: () => of(summary) } as unknown as PortfolioService);
    component.editAccount(account);
    component.selectedId.set(account.id);
    component.accountName = 'Updated savings';
    component.saveAccount();
    component.loadAccounts();
    saved.next({ ...account, name: 'Updated savings' });
    newer.next([{ ...account, name: 'Updated savings' }]);
    older.next([account]);
    expect(component.selectedAccounts[0].name).toBe('Updated savings');
    expect(component.saving()).toBe(false);
  });

  it('distinguishes a failed catalog load from a genuinely empty catalog', () => {
    const component = new Catalog({ getInvestments: () => throwError(() => failure) } as unknown as PortfolioService);
    component.load();
    expect(component.loadError()).toContain('Server temporarily unavailable');
    expect(component.loading()).toBe(false);
  });

  it('distinguishes a failed portfolio load from an account with no portfolios', () => {
    const component = new Workspace({ getPortfolios: () => throwError(() => failure) } as unknown as PortfolioService);
    component.account = account;
    component.loadPortfolios();
    expect(component.portfoliosError()).toContain('Server temporarily unavailable');
    expect(component.loading()).toBe(false);
  });

  it('ignores a stale catalog load after a newer one has completed', () => {
    const older = new Subject<Investment[]>();
    const newer = new Subject<Investment[]>();
    const service = { getInvestments: vi.fn().mockReturnValueOnce(older).mockReturnValueOnce(newer) };
    const component = new Catalog(service as unknown as PortfolioService);
    component.load(); component.load();
    newer.next([{ id: 1, symbol: 'NEW', name: 'Current', currentPrice: 20 }]);
    older.error(failure);
    expect(component.investments()[0].currentPrice).toBe(20);
    expect(component.loadError()).toBe('');
  });
});

describe('Review regressions: session failure feedback', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reports a startup server failure instead of silently treating it as logged out', () => {
    const component = new App({ getUser: () => throwError(() => failure) } as unknown as AuthService);
    component.ngOnInit();
    expect(component.ready()).toBe(true);
    expect(component.message()).toContain('Server temporarily unavailable');
  });

  it('shows the normal login state without an error for an anonymous visitor', () => {
    const component = new App({ getUser: () => throwError(() => new HttpErrorResponse({ status: 401 })) } as unknown as AuthService);
    component.ngOnInit();
    expect(component.ready()).toBe(true);
    expect(component.message()).toBe('');
  });

  it('renders logout failures while keeping the authenticated workspace visible', async () => {
    const auth = {
      getUser: () => of({ id: '1', email: 'test@example.test', roles: ['Customer'] }),
      refreshCsrf: () => of({}), logout: () => throwError(() => failure)
    };
    await TestBed.configureTestingModule({ imports: [App], providers: [
      { provide: AuthService, useValue: auth },
      { provide: AccountService, useValue: { getAccounts: () => of([]) } },
      { provide: PortfolioService, useValue: { getDashboard: () => of(summary) } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    fixture.componentInstance.logout();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Server temporarily unavailable');
    expect(fixture.componentInstance.user()).not.toBeNull();
  });
});
