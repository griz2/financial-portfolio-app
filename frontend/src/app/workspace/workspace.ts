import { Component, EventEmitter, Input, OnInit, Output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { FinancialAccount } from '../models/financial-account';
import { Transaction, Holding, Investment, PortfolioDetails } from '../models/portfolio';
import { PortfolioService } from '../services/portfolio-service';
import { errorMessage } from '../services/error-message';

@Component({
  selector: 'app-workspace',
  imports: [FormsModule, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './workspace.html'
})
export class Workspace implements OnInit {
  @Input({ required: true }) account!: FinancialAccount;
  @Output() changed = new EventEmitter<void>();
  portfolios = signal<PortfolioDetails[]>([]);
  holdings = signal<Holding[]>([]);
  investments = signal<Investment[]>([]);
  history = signal<Transaction[]>([]);
  loading = signal(true);
  holdingsLoading = signal(false);
  historyLoading = signal(false);
  holdingsError = signal('');
  portfoliosError = signal('');
  historyError = signal('');
  private holdingsRequest: number = 0;
  private historyRequest: number = 0;
  private portfoliosRequest: number = 0;
  busy = signal(false);
  message = signal('');
  portfolioId: number | null = null;
  portfolioName: string = '';
  editingPortfolioId: number | null = null;
  cashType: string = 'Deposit';
  amount: number | null = null;
  tradeType: string = 'Buy';
  investmentId: number | null = null;
  quantity: number | null = null;

  constructor(private service: PortfolioService) {
  }

  ngOnInit(): void {
    this.loadPortfolios();
    this.loadHistory();
    this.service.getInvestments().subscribe({
      next: rows => {
        this.investments.set(rows);
        this.investmentId = rows[0]?.id ?? null;
      },
      error: error => this.message.set(errorMessage(error))
    });
  }

  loadPortfolios(): void {
    const requestId = ++this.portfoliosRequest;
    this.portfoliosError.set('');
    this.loading.set(true);
    this.service.getPortfolios(this.account.id).subscribe({
      next: rows => {
        if (requestId !== this.portfoliosRequest) {
          return;
        }
        this.portfolios.set(rows);
        if (!rows.some(p => p.id === this.portfolioId)) {
          this.portfolioId = rows[0]?.id ?? null;
        }
        this.loading.set(false);
        this.loadHoldings();
      },
      error: error => {
        if (requestId !== this.portfoliosRequest) {
          return;
        }
        this.portfoliosError.set(errorMessage(error));
        this.loading.set(false);
      }
    });
  }

  editPortfolio(): void {
    const portfolio = this.portfolios().find(p => p.id === this.portfolioId);
    if (portfolio) {
      this.editingPortfolioId = portfolio.id;
      this.portfolioName = portfolio.name;
    }
  }

  clearPortfolioForm(): void {
    this.editingPortfolioId = null;
    this.portfolioName = '';
  }

  savePortfolio(): void {
    const name = this.portfolioName.trim();
    if (this.busy() || !name || name.length > 100) {
      return;
    }
    this.busy.set(true);
    this.message.set('');
    let request: Observable<PortfolioDetails>;
    if (this.editingPortfolioId === null) {
      request = this.service.createPortfolio(this.account.id, name);
    } else {
      request = this.service.renamePortfolio(this.account.id, this.editingPortfolioId, name);
    }
    request.subscribe({
      next: portfolio => {
        this.portfolioId = portfolio.id;
        this.clearPortfolioForm();
        this.busy.set(false);
        this.message.set('Portfolio saved.');
        this.loadPortfolios();
        this.changed.emit();
      },
      error: error => {
        this.message.set(errorMessage(error));
        this.busy.set(false);
      }
    });
  }

  loadHoldings(): void {
    const requestId = ++this.holdingsRequest;
    this.holdingsError.set('');
    this.holdings.set([]);
    if (this.portfolioId === null) {
      this.holdingsLoading.set(false);
      return;
    }
    this.holdingsLoading.set(true);
    this.service.getHoldings(this.account.id, this.portfolioId).subscribe({
      next: rows => {
        if (requestId !== this.holdingsRequest) {
          return;
        }
        this.holdings.set(rows);
        this.holdingsLoading.set(false);
      },
      error: error => {
        if (requestId !== this.holdingsRequest) {
          return;
        }
        this.holdingsError.set(errorMessage(error));
        this.holdingsLoading.set(false);
      }
    });
  }

  saveCash(): void {
    if (this.busy() || this.amount === null) {
      return;
    }
    this.busy.set(true);
    this.message.set('');
    this.service.recordCash(this.account.id, this.cashType, this.amount).subscribe({
      next: () => {
        this.amount = null;
        this.transactionSaved();
      },
      error: error => {
        this.message.set(errorMessage(error));
        this.busy.set(false);
      }
    });
  }

  saveTrade(): void {
    if (this.busy() || this.portfolioId === null || this.investmentId === null || this.quantity === null) {
      return;
    }
    this.busy.set(true);
    this.message.set('');
    this.service.recordTrade(this.account.id, this.portfolioId, this.investmentId, this.tradeType, this.quantity).subscribe({
      next: () => {
        this.quantity = null;
        this.transactionSaved();
      },
      error: error => {
        this.message.set(errorMessage(error));
        this.busy.set(false);
      }
    });
  }

  private transactionSaved(): void {
    this.busy.set(false);
    this.message.set('Transaction recorded.');
    this.changed.emit();
    this.loadHoldings();
    this.loadHistory();
  }

  loadHistory(): void {
    const requestId = ++this.historyRequest;
    this.historyError.set('');
    this.historyLoading.set(true);
    this.service.getHistory(this.account.id).subscribe({
      next: result => {
        if (requestId !== this.historyRequest) {
          return;
        }
        this.history.set(result);
        this.historyLoading.set(false);
      },
      error: error => {
        if (requestId !== this.historyRequest) {
          return;
        }
        this.history.set([]);
        this.historyError.set(errorMessage(error));
        this.historyLoading.set(false);
      }
    });
  }
}
