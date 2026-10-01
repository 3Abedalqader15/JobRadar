import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from './auth.service';
import { Router, RouterModule } from '@angular/router';

type Mode = 'login' | 'register';
type UserRole = 'User' | 'HR';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './auth.component.html',
  styleUrls: ['./auth.component.css']
})
export class AuthComponent {
  mode: Mode = 'login';
  selectedRole: UserRole = 'User';
  error: string | null = null;
  loading = false;

  loginForm: FormGroup;
  registerForm: FormGroup;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
    });

    this.registerForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
    });

    // If already logged in, redirect
    if (this.authService.isLoggedIn()) {
      this.redirectAfterLogin();
    }
  }

  setMode(mode: Mode) {
    this.mode = mode;
    this.error = null;
  }

  setRole(role: UserRole) {
    this.selectedRole = role;
  }

  get isLogin() { return this.mode === 'login'; }
  get isRegister() { return this.mode === 'register'; }

  onLogin() {
    if (this.loginForm.invalid) { this.loginForm.markAllAsTouched(); return; }
    this.loading = true;
    this.error = null;
    const { email, password } = this.loginForm.value;

    this.authService.login({ email, password }).subscribe({
      next: () => {
        this.loading = false;
        this.redirectAfterLogin();
      },
      error: err => {
        this.loading = false;
        this.error = err.error?.errors?.[0] ?? err.error?.message ?? 'Invalid credentials. Please try again.';
      }
    });
  }

  onRegister() {
    if (this.registerForm.invalid) { this.registerForm.markAllAsTouched(); return; }
    this.loading = true;
    this.error = null;
    const { fullName, email, password } = this.registerForm.value;

    this.authService.register({ fullName, email, password, role: this.selectedRole }).subscribe({
      next: () => {
        this.loading = false;
        // Auto-login after register
        this.authService.login({ email, password }).subscribe({
          next: () => this.redirectAfterLogin(),
          error: () => {
            this.setMode('login');
            this.error = 'Registration successful! Please login.';
          }
        });
      },
      error: err => {
        this.loading = false;
        const errors = err.error?.errors;
        this.error = Array.isArray(errors) ? errors[0] : (err.error?.message ?? 'Registration failed. Please try again.');
      }
    });
  }

  private redirectAfterLogin() {
    if (this.authService.isAdminOrHR()) {
      this.router.navigate(['/admin']);
    } else {
      this.router.navigate(['/']);
    }
  }
}
