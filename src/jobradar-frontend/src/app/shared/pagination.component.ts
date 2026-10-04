import { Component, Input, Output, EventEmitter, computed, signal, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="pagination-wrapper" *ngIf="totalCount > 0">
      <!-- Left: Range Info -->
      <div class="pagination-info">
        Showing
        <span class="info-highlight">{{ startItem }} - {{ endItem }}</span>
        of
        <span class="info-highlight">{{ totalCount }}</span>
        {{ itemLabel }}
      </div>

      <!-- Center: Navigation controls -->
      <div class="pagination-controls">
        <button
          type="button"
          class="page-btn nav-btn"
          [disabled]="currentPage === 1"
          (click)="onPageClick(1)"
          title="First Page"
        >
          ««
        </button>

        <button
          type="button"
          class="page-btn nav-btn"
          [disabled]="currentPage === 1"
          (click)="onPageClick(currentPage - 1)"
          title="Previous Page"
        >
          ‹ Prev
        </button>

        <div class="page-numbers">
          @for (page of visiblePages(); track $index) {
            @if (page === -1) {
              <span class="ellipsis">...</span>
            } @else {
              <button
                type="button"
                class="page-btn num-btn"
                [class.active]="page === currentPage"
                (click)="onPageClick(page)"
              >
                {{ page }}
              </button>
            }
          }
        </div>

        <button
          type="button"
          class="page-btn nav-btn"
          [disabled]="currentPage >= totalPages()"
          (click)="onPageClick(currentPage + 1)"
          title="Next Page"
        >
          Next ›
        </button>

        <button
          type="button"
          class="page-btn nav-btn"
          [disabled]="currentPage >= totalPages()"
          (click)="onPageClick(totalPages())"
          title="Last Page"
        >
          »»
        </button>
      </div>

      <!-- Right: Page Size Selector & Custom Input -->
      <div class="pagination-size-box">
        <label for="pageSizeSelect" class="size-label">Per page:</label>
        <select
          id="pageSizeSelect"
          class="size-select"
          [ngModel]="isCustomSize ? 'custom' : pageSize"
          (ngModelChange)="onSelectSizeChange($event)"
        >
          @for (option of pageSizeOptions; track option) {
            <option [value]="option">{{ option }}</option>
          }
          <option value="custom">Custom...</option>
        </select>

        @if (isCustomSize || showCustomInput) {
          <div class="custom-size-wrap">
            <input
              type="number"
              class="custom-size-input"
              [(ngModel)]="customInputValue"
              (keydown.enter)="applyCustomSize()"
              placeholder="#"
              min="1"
              max="500"
              title="Enter custom items count and click Apply or press Enter"
            />
            <button
              type="button"
              class="btn-apply-size"
              (click)="applyCustomSize()"
              title="Apply custom item count"
            >
              Apply
            </button>
          </div>
        }
      </div>
    </div>
  `,
  styleUrls: ['./pagination.component.css']
})
export class PaginationComponent implements OnChanges {
  @Input() currentPage: number = 1;
  @Input() pageSize: number = 20;
  @Input() totalCount: number = 0;
  @Input() pageSizeOptions: number[] = [10, 20, 50, 100];
  @Input() itemLabel: string = 'items';
  @Input() showCustomInput: boolean = true;

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  customInputValue: number = 20;
  isCustomSize: boolean = false;

  totalPages = computed(() => {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  });

  get startItem(): number {
    if (this.totalCount === 0) return 0;
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get endItem(): number {
    return Math.min(this.currentPage * this.pageSize, this.totalCount);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['pageSize']) {
      this.customInputValue = this.pageSize;
      this.isCustomSize = !this.pageSizeOptions.includes(this.pageSize);
    }
  }

  visiblePages = computed<number[]>(() => {
    const total = this.totalPages();
    const current = this.currentPage;
    const pages: number[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) {
        pages.push(i);
      }
      return pages;
    }

    pages.push(1);

    if (current > 3) {
      pages.push(-1); // Ellipsis
    }

    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    if (current < total - 2) {
      pages.push(-1); // Ellipsis
    }

    pages.push(total);

    return pages;
  });

  onPageClick(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.currentPage) {
      this.pageChange.emit(page);
    }
  }

  onSelectSizeChange(value: string | number): void {
    if (value === 'custom') {
      this.isCustomSize = true;
      return;
    }
    const num = Number(value);
    if (!isNaN(num) && num > 0) {
      this.isCustomSize = false;
      this.pageSizeChange.emit(num);
    }
  }

  applyCustomSize(): void {
    const num = Number(this.customInputValue);
    if (!isNaN(num) && num > 0 && num <= 500) {
      this.isCustomSize = true;
      this.pageSizeChange.emit(num);
    }
  }
}
