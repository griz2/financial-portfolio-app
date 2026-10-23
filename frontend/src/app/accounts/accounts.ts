import { Component, OnInit, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AccountService } from '../services/account-service';
import { FinancialAccount } from '../models/financial-account';
import { Workspace } from '../workspace/workspace';
import { Dashboard } from '../models/portfolio';
import { PortfolioService } from '../services/portfolio-service';
import { errorMessage } from '../services/error-message';

@Component({
  selector: 'app-accounts',
  imports: [FormsModule, CurrencyPipe, Workspace],
  templateUrl: './accounts.html',
  styleUrl: './accounts.css'
})
export class Accounts implements OnInit {
  accounts = signal<FinancialAccount[]>([]);
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  loadError = signal<string>('');
  formError = signal<string>('');
  successMessage = signal<string>('');
  editingId = signal<number | null>(null);
  accountName: string = '';
  selectedId = signal<number | null>(null);
  dashboardLoading = signal(true);
  dashboardError = signal('');
  private accountsRequest: number = 0;
  private dashboardRequest: number = 0;
  dashboard = signal<Dashboard>({
    cashBalance: 0,
    investmentValue: 0,
    totalValue: 0,
    accountCount: 0,
    portfolioCount: 0
  });

  constructor(private accountService: AccountService, private portfolioService: PortfolioService) {
  }

  get selectedAccounts(): FinancialAccount[] {
    return this.accounts().filter(account => account.id === this.selectedId());
  }

  ngOnInit(): void {
    this.loadAccounts();
  }

  loadAccounts(): void {
    const requestId = ++this.accountsRequest;
    this.loading.set(true);
    this.loadError.set('');
    this.accountService.getAccounts().subscribe({
      next: accounts => {
        if (requestId !== this.accountsRequest) {
          return;
        }
        this.accounts.set(accounts);
        this.loading.set(false);
        this.refreshDashboard();
      },
      error: error => {
        if (requestId !== this.accountsRequest) {
          return;
        }
        this.loadError.set(errorMessage(error));
        this.loading.set(false);
      }
    });
  }

  saveAccount(): void {
    if (this.saving() || this.loading()) {
      return;
    }

    const name: string = this.accountName.trim();
    this.formError.set('');
    this.successMessage.set('');

    if (name.length === 0 || name.length > 100) {
      this.formError.set('Enter an account name containing 1 to 100 characters.');
      return;
    }

    const id: number | null = this.editingId();
    let request: Observable<FinancialAccount>;
    if (id === null) {
      request = this.accountService.createAccount(name);
    } else {
      request = this.accountService.renameAccount(id, name);
    }

    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.cancelEdit();
        if (id === null) {
          this.successMessage.set('Account created.');
        } else {
          this.successMessage.set('Account name updated.');
        }
        this.loadAccounts();
      },
      error: (error: HttpErrorResponse) => {
        this.formError.set(errorMessage(error));
        this.saving.set(false);
      }
    });
  }

  editAccount(account: FinancialAccount): void {
    this.editingId.set(account.id);
    this.accountName = account.name;
    this.formError.set('');
    this.successMessage.set('');
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.accountName = '';
    this.formError.set('');
  }

  refreshDashboard(): void {
    const requestId = ++this.dashboardRequest;
    this.dashboardLoading.set(true);
    this.dashboardError.set('');
    this.portfolioService.getDashboard().subscribe({
      next: result => {
        if (requestId !== this.dashboardRequest) {
          return;
        }
        this.dashboard.set(result);
        this.dashboardLoading.set(false);
      },
      error: error => {
        if (requestId !== this.dashboardRequest) {
          return;
        }
        this.dashboardError.set(errorMessage(error));
        this.dashboardLoading.set(false);
      }
    });
  }
}
