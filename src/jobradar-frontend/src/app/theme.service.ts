import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly storageKey = 'jobradar_theme';
  
  // Default to 'light' mode on first load for all new visitors
  public currentTheme = signal<Theme>('light');

  constructor() {
    this.initTheme();
  }

  private initTheme(): void {
    const saved = localStorage.getItem(this.storageKey) as Theme | null;
    if (saved === 'light' || saved === 'dark') {
      this.setTheme(saved);
    } else {
      // Default to Light Mode for new/first-time users
      this.setTheme('light');
    }
  }

  public toggleTheme(): void {
    const next = this.currentTheme() === 'dark' ? 'light' : 'dark';
    this.setTheme(next);
  }

  public setTheme(theme: Theme): void {
    this.currentTheme.set(theme);
    localStorage.setItem(this.storageKey, theme);
    
    if (theme === 'dark') {
      document.documentElement.setAttribute('data-theme', 'dark');
      document.documentElement.classList.add('dark');
      document.documentElement.classList.remove('light');
    } else {
      // Light Mode default: remove data-theme or set to light
      document.documentElement.removeAttribute('data-theme');
      document.documentElement.classList.add('light');
      document.documentElement.classList.remove('dark');
    }
  }
}
