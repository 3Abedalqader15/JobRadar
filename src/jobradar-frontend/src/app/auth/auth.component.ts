import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './auth.component.html',
  styles: [`
    .auth-container { max-width: 400px; margin: 40px auto; padding: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); border-radius: 8px; background: white; }
    .form-group { margin-bottom: 15px; }
    .form-group label { display: block; margin-bottom: 5px; font-weight: bold; }
    .form-control { width: 100%; padding: 8px; border: 1px solid #ccc; border-radius: 4px; }
    .btn { width: 100%; padding: 10px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }
    .btn:hover { background: #0056b3; }
    .toggle-link { text-align: center; margin-top: 15px; color: #007bff; cursor: pointer; }
    .error { color: red; font-size: 0.9em; margin-bottom: 10px; }
  `]
})
export class AuthComponent {
  authForm: FormGroup;
  isLoginMode = true;
  error: string | null = null;
  loading = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.authForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      fullName: ['']
    });
  }

  toggleMode() {
    this.isLoginMode = !this.isLoginMode;
    if (this.isLoginMode) {
      this.authForm.get('fullName')?.clearValidators();
    } else {
      this.authForm.get('fullName')?.setValidators([Validators.required]);
    }
    this.authForm.get('fullName')?.updateValueAndValidity();
    this.error = null;
  }

  onSubmit() {
    if (this.authForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = null;
    const val = this.authForm.value;

    const authObs = this.isLoginMode
      ? this.authService.login({ email: val.email, password: val.password })
      : this.authService.register({ email: val.email, password: val.password, fullName: val.fullName });

    authObs.subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/']); // Redirect to home/job feed
      },
      error: (err) => {
        this.loading = false;
        if (err.error && err.error.message) {
           this.error = err.error.message;
        } else if (err.error && err.error.error) {
           this.error = err.error.error;
        } else {
           this.error = 'An error occurred during authentication.';
        }
      }
    });
  }
}
