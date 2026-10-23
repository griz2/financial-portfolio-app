import { Component, Input, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { Investment } from '../models/portfolio';
import { PortfolioService } from '../services/portfolio-service';
import { errorMessage } from '../services/error-message';

@Component({
  selector: 'app-catalog',
  imports: [FormsModule, CurrencyPipe],
  templateUrl: './catalog.html'
})
export class Catalog implements OnInit {
  @Input() isAdmin: boolean = false;
  investments = signal<Investment[]>([]);
  loading = signal(false);
  busy = signal(false);
  message = signal('');
  loadError = signal('');
  private loadRequest: number = 0;
  symbol: string = '';
  name: string = '';
  price: number | null = null;
  editingId = signal<number | null>(null);

  constructor(private service: PortfolioService) {
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const requestId = ++this.loadRequest;
    this.loading.set(true);
    this.loadError.set('');
    this.service.getInvestments().subscribe({
      next: rows => {
        if (requestId !== this.loadRequest) {
          return;
        }
        this.investments.set(rows);
        this.loading.set(false);
      },
      error: error => {
        if (requestId !== this.loadRequest) {
          return;
        }
        this.loadError.set(errorMessage(error));
        this.loading.set(false);
      }
    });
  }
  edit(investment: Investment): void {
    this.editingId.set(investment.id);
    this.symbol = investment.symbol;
    this.name = investment.name;
    this.price = investment.currentPrice;
    this.message.set('');
  }

  cancel(): void {
    this.editingId.set(null);
    this.symbol = '';
    this.name = '';
    this.price = null;
  }

  save(): void {
    if (this.busy() || this.price === null) {
      return;
    }

    const id = this.editingId();
    this.busy.set(true);
    this.message.set('');
    let request: Observable<Investment>;
    if (id === null) {
      request = this.service.createInvestment(this.symbol.trim(), this.name.trim(), this.price);
    } else {
      request = this.service.updateInvestment(id, this.price);
    }

    request.subscribe({
      next: () => {
        this.cancel();
        this.busy.set(false);
        this.message.set('Investment saved.');
        this.load();
      },
      error: error => {
        this.message.set(errorMessage(error));
        this.busy.set(false);
      }
    });
  }
}
