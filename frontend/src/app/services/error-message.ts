import { HttpErrorResponse } from '@angular/common/http';

export function errorMessage(error: HttpErrorResponse): string {
  if (error.status === 401) {
    return 'Your session has ended. Please sign out and log in again.';
  }

  if (error.status === 0) {
    return 'The server could not be reached. Check the connection before submitting again.';
  }

  if (error.error?.errors) {
    const messages = Object.values(error.error.errors).flat();
    if (messages.length > 0) {
      return String(messages[0]);
    }

  }
  return error.error?.detail ?? error.error?.title ?? 'The operation could not be completed.';
}

