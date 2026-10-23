import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Accounts } from './accounts/accounts';
import { Catalog } from './catalog/catalog';
import { AuthService } from './services/auth-service';
import { errorMessage } from './services/error-message';
import { UserDetails } from './models/portfolio';

@Component({
  selector: 'app-root',
  imports: [Accounts, Catalog, FormsModule],
  templateUrl: './app.html'
})
export class App implements OnInit {
  user = signal<UserDetails | null>(null);
  ready = signal(false);
  busy = signal(false);
  message = signal('');
  registration: boolean = false;
  email: string = '';
  password: string = '';
  view = signal('workspace');

  constructor(private auth: AuthService) {
  }

  ngOnInit(): void {
    this.auth.getUser().subscribe({
      next: user => {
        this.auth.refreshCsrf().subscribe({
          next: () => {
            this.user.set(user);
            this.ready.set(true);
          },
          error: error => {
            this.message.set(errorMessage(error));
            this.ready.set(true);
          }
        });
      },
      error: error => {
        if (error.status !== 401) {
          this.message.set(errorMessage(error));
        }
        this.ready.set(true);
      }
    });
  }

  submit(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.message.set('');
    if (this.registration) {
      this.auth.register(this.email, this.password).subscribe({
        next: () => {
          this.registration = false;
          this.password = '';
          this.message.set('Registration complete. Please log in.');
          this.busy.set(false);
        },
        error: error => {
          this.message.set(errorMessage(error));
          this.busy.set(false);
        }
      });
    } else {
      this.auth.login(this.email, this.password).subscribe({
        next: user => {
          this.user.set(user);
          this.password = '';
          this.busy.set(false);
          this.view.set('workspace');
        },
        error: error => {
          this.message.set(error.status === 401
            ? 'Check your credentials or try again later.'
            : errorMessage(error));
          this.busy.set(false);
        }
      });
    }
  }

  logout(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.auth.logout().subscribe({
      next: () => {
        this.user.set(null);
        this.message.set('');
        this.busy.set(false);
      },
      error: error => {
        if (error.status === 401) {
          this.user.set(null);
        }
        this.message.set(errorMessage(error));
        this.busy.set(false);
      }
    });
  }
}

