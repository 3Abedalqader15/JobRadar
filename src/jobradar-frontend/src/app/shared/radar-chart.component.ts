import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface RadarMetrics {
  skills: number;     // 0 - 100
  experience: number; // 0 - 100
  location: number;   // 0 - 100
  seniority: number;  // 0 - 100
  workplace: number;  // 0 - 100 (Remote / On-site fit)
}

interface AxisConfig {
  key: keyof RadarMetrics;
  label: string;
  angle: number;
}

@Component({
  selector: 'app-radar-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (mode === 'compact') {
      <div
        class="radar-compact"
        [title]="summaryTooltip"
        [attr.aria-label]="summaryTooltip"
        role="img"
      >
        <svg viewBox="0 0 100 100" class="radar-compact-svg" [style.width.px]="size" [style.height.px]="size">
          <!-- Background pentagon -->
          <polygon [attr.points]="backgroundPolygonPoints(100)" class="radar-ring-bg" />
          <!-- Mid ring -->
          <polygon [attr.points]="backgroundPolygonPoints(60)" class="radar-ring-mid" />
          <!-- Data Polygon -->
          <polygon [attr.points]="dataPolygonPoints()" class="radar-data-compact" />
        </svg>
        <span class="compact-score">{{ overallScore }}%</span>
      </div>
    } @else {
      <div class="radar-full-container" role="region" aria-label="Job DNA Match Radar Chart">
        <div class="radar-header">
          <div class="radar-title-group">
            <span class="radar-badge">DNA Match</span>
            <span class="radar-score">{{ overallScore }}% Fit</span>
          </div>
          <p class="radar-subtitle">Multidimensional candidate-to-job compatibility</p>
        </div>

        <div class="radar-svg-wrapper">
          <svg viewBox="0 0 320 300" class="radar-svg" [style.max-width.px]="size">
            <!-- Concentric rings -->
            @for (ring of [20, 40, 60, 80, 100]; track ring) {
              <polygon
                [attr.points]="backgroundPolygonPointsFull(ring)"
                class="radar-grid-ring"
                [class.radar-grid-ring-outer]="ring === 100"
              />
            }

            <!-- Axis lines -->
            @for (axis of axes; track axis.key) {
              <line
                [attr.x1]="centerFull.x"
                [attr.y1]="centerFull.y"
                [attr.x2]="axisEndpoint(axis.angle, 100).x"
                [attr.y2]="axisEndpoint(axis.angle, 100).y"
                class="radar-axis-line"
              />
            }

            <!-- Data Polygon -->
            <polygon [attr.points]="dataPolygonPointsFull()" class="radar-data-polygon" />

            <!-- Vertex Dots & Hover Targets -->
            @for (axis of axes; track axis.key) {
              <circle
                [attr.cx]="dataPoint(axis).x"
                [attr.cy]="dataPoint(axis).y"
                r="4.5"
                class="radar-data-dot"
              />
            }

            <!-- Axis Labels -->
            @for (axis of axes; track axis.key) {
              <text
                [attr.x]="labelPosition(axis.angle).x"
                [attr.y]="labelPosition(axis.angle).y"
                [attr.text-anchor]="labelAnchor(axis.angle)"
                class="radar-axis-label"
              >
                {{ axis.label }}
                <tspan class="radar-axis-val" [attr.x]="labelPosition(axis.angle).x" dy="13">
                  {{ safeMetrics[axis.key] }}%
                </tspan>
              </text>
            }
          </svg>
        </div>

        <!-- Metric breakdown bars for enhanced accessibility / scanning -->
        <div class="radar-breakdown">
          @for (axis of axes; track axis.key) {
            <div class="breakdown-item">
              <div class="breakdown-header">
                <span class="breakdown-name">{{ axis.label }}</span>
                <span class="breakdown-value">{{ safeMetrics[axis.key] }}%</span>
              </div>
              <div class="breakdown-track">
                <div class="breakdown-fill" [style.width.%]="safeMetrics[axis.key]"></div>
              </div>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    :host {
      display: block;
    }

    /* ── Compact Variant (List & Grid Badges) ────────────────────────── */
    .radar-compact {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      padding: 3px var(--space-2);
      border-radius: var(--radius-full);
      background-color: var(--color-surface-hover);
      border: 1px solid var(--color-border);
      cursor: help;
      transition: background-color var(--duration-fast) var(--ease-base);
    }
    .radar-compact:hover {
      background-color: var(--color-surface);
      border-color: var(--color-primary);
    }

    .radar-compact-svg {
      overflow: visible;
      flex-shrink: 0;
    }

    .radar-ring-bg {
      fill: none;
      stroke: var(--color-border);
      stroke-width: 2;
    }
    .radar-ring-mid {
      fill: none;
      stroke: var(--color-border-subtle);
      stroke-width: 1.5;
      stroke-dasharray: 2 2;
    }
    .radar-data-compact {
      fill: hsla(var(--primary-h), var(--primary-s), var(--primary-l), 0.25);
      stroke: var(--color-primary);
      stroke-width: 3;
      stroke-linejoin: round;
    }

    .compact-score {
      font-family: var(--font-mono);
      font-size: var(--text-xs);
      font-weight: 600;
      color: var(--color-text);
      line-height: 1;
    }

    /* ── Full Variant (Job Detail Drawer) ───────────────────────────── */
    .radar-full-container {
      background-color: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: var(--space-4);
      margin-block-end: var(--space-4);
    }

    .radar-header {
      margin-block-end: var(--space-3);
    }

    .radar-title-group {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2);
    }

    .radar-badge {
      display: inline-flex;
      align-items: center;
      font-size: var(--text-xs);
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--color-primary);
      background-color: hsla(var(--primary-h), var(--primary-s), var(--primary-l), 0.1);
      padding: 2px var(--space-2);
      border-radius: var(--radius-sm);
    }

    .radar-score {
      font-family: var(--font-mono);
      font-size: var(--text-sm);
      font-weight: 700;
      color: var(--color-text);
    }

    .radar-subtitle {
      font-size: var(--text-xs);
      color: var(--color-text-muted);
      margin-block-start: var(--space-1);
    }

    .radar-svg-wrapper {
      display: flex;
      justify-content: center;
      align-items: center;
      padding: var(--space-2) 0;
    }

    .radar-svg {
      width: 100%;
      height: auto;
      overflow: visible;
    }

    .radar-grid-ring {
      fill: none;
      stroke: var(--color-border-subtle);
      stroke-width: 1;
    }

    .radar-grid-ring-outer {
      stroke: var(--color-border);
      stroke-width: 1.5;
    }

    .radar-axis-line {
      stroke: var(--color-border-subtle);
      stroke-width: 1;
    }

    .radar-data-polygon {
      fill: hsla(var(--primary-h), var(--primary-s), var(--primary-l), 0.2);
      stroke: var(--color-primary);
      stroke-width: 2.5;
      stroke-linejoin: round;
      transition: all var(--duration-base) var(--ease-base);
    }

    .radar-data-dot {
      fill: var(--color-surface);
      stroke: var(--color-primary);
      stroke-width: 2.5;
      transition: r var(--duration-fast) var(--ease-base);
    }
    .radar-data-dot:hover {
      r: 6;
      fill: var(--color-primary);
    }

    .radar-axis-label {
      font-size: 10.5px;
      font-weight: 500;
      fill: var(--color-text-muted);
      letter-spacing: -0.01em;
    }

    .radar-axis-val {
      font-family: var(--font-mono);
      font-size: 10px;
      font-weight: 600;
      fill: var(--color-text);
    }

    /* Metric breakdown progress bars */
    .radar-breakdown {
      display: grid;
      grid-template-columns: 1fr;
      gap: var(--space-2);
      margin-block-start: var(--space-3);
      padding-block-start: var(--space-3);
      border-block-start: 1px solid var(--color-border-subtle);
    }

    .breakdown-header {
      display: flex;
      justify-content: space-between;
      font-size: var(--text-xs);
      margin-block-end: 2px;
    }

    .breakdown-name {
      color: var(--color-text-muted);
    }

    .breakdown-value {
      font-family: var(--font-mono);
      font-weight: 600;
      color: var(--color-text);
    }

    .breakdown-track {
      height: 4px;
      background-color: var(--color-surface-hover);
      border-radius: var(--radius-full);
      overflow: hidden;
    }

    .breakdown-fill {
      height: 100%;
      background-color: var(--color-primary);
      border-radius: var(--radius-full);
      transition: width var(--duration-base) var(--ease-base);
    }

    /* Prefers-reduced-motion accessibility */
    @media (prefers-reduced-motion: reduce) {
      .radar-data-polygon,
      .radar-data-dot,
      .breakdown-fill {
        transition: none !important;
      }
    }
  `]
})
export class RadarChartComponent {
  @Input() metrics?: Partial<RadarMetrics>;
  @Input() mode: 'full' | 'compact' = 'full';
  @Input() size: number = 260;

  public readonly axes: AxisConfig[] = [
    { key: 'skills', label: 'Skills Overlap', angle: -Math.PI / 2 },
    { key: 'experience', label: 'Experience Fit', angle: -Math.PI / 2 + (2 * Math.PI) / 5 },
    { key: 'location', label: 'Location Fit', angle: -Math.PI / 2 + (4 * Math.PI) / 5 },
    { key: 'seniority', label: 'Seniority Fit', angle: -Math.PI / 2 + (6 * Math.PI) / 5 },
    { key: 'workplace', label: 'Workplace / Remote', angle: -Math.PI / 2 + (8 * Math.PI) / 5 }
  ];

  // Centers and radii
  public readonly centerCompact = { x: 50, y: 50 };
  public readonly radiusCompact = 42;

  public readonly centerFull = { x: 160, y: 145 };
  public readonly radiusFull = 95;

  public get safeMetrics(): RadarMetrics {
    return {
      skills: this.clamp(this.metrics?.skills ?? 80),
      experience: this.clamp(this.metrics?.experience ?? 75),
      location: this.clamp(this.metrics?.location ?? 85),
      seniority: this.clamp(this.metrics?.seniority ?? 70),
      workplace: this.clamp(this.metrics?.workplace ?? 90)
    };
  }

  public get overallScore(): number {
    const m = this.safeMetrics;
    const avg = (m.skills + m.experience + m.location + m.seniority + m.workplace) / 5;
    return Math.round(avg);
  }

  public get summaryTooltip(): string {
    const m = this.safeMetrics;
    return `DNA Match: ${this.overallScore}% (Skills ${m.skills}%, Exp ${m.experience}%, Loc ${m.location}%, Sen ${m.seniority}%, Remote ${m.workplace}%)`;
  }

  private clamp(val: number): number {
    if (isNaN(val)) return 50;
    return Math.max(10, Math.min(100, Math.round(val)));
  }

  // --- COMPACT CALCULATIONS ---
  public backgroundPolygonPoints(pct: number): string {
    const r = (this.radiusCompact * pct) / 100;
    return this.axes
      .map(axis => {
        const x = this.centerCompact.x + r * Math.cos(axis.angle);
        const y = this.centerCompact.y + r * Math.sin(axis.angle);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  public dataPolygonPoints(): string {
    const m = this.safeMetrics;
    return this.axes
      .map(axis => {
        const val = m[axis.key];
        const r = (this.radiusCompact * val) / 100;
        const x = this.centerCompact.x + r * Math.cos(axis.angle);
        const y = this.centerCompact.y + r * Math.sin(axis.angle);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  // --- FULL CALCULATIONS ---
  public backgroundPolygonPointsFull(pct: number): string {
    const r = (this.radiusFull * pct) / 100;
    return this.axes
      .map(axis => {
        const x = this.centerFull.x + r * Math.cos(axis.angle);
        const y = this.centerFull.y + r * Math.sin(axis.angle);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  public dataPolygonPointsFull(): string {
    const m = this.safeMetrics;
    return this.axes
      .map(axis => {
        const val = m[axis.key];
        const r = (this.radiusFull * val) / 100;
        const x = this.centerFull.x + r * Math.cos(axis.angle);
        const y = this.centerFull.y + r * Math.sin(axis.angle);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  public axisEndpoint(angle: number, pct: number): { x: number; y: number } {
    const r = (this.radiusFull * pct) / 100;
    return {
      x: +(this.centerFull.x + r * Math.cos(angle)).toFixed(1),
      y: +(this.centerFull.y + r * Math.sin(angle)).toFixed(1)
    };
  }

  public dataPoint(axis: AxisConfig): { x: number; y: number } {
    const val = this.safeMetrics[axis.key];
    const r = (this.radiusFull * val) / 100;
    return {
      x: +(this.centerFull.x + r * Math.cos(axis.angle)).toFixed(1),
      y: +(this.centerFull.y + r * Math.sin(axis.angle)).toFixed(1)
    };
  }

  public labelPosition(angle: number): { x: number; y: number } {
    const offset = 26;
    const r = this.radiusFull + offset;
    let x = this.centerFull.x + r * Math.cos(angle);
    let y = this.centerFull.y + r * Math.sin(angle);

    // Minor visual offsets for readability
    if (Math.abs(angle - (-Math.PI / 2)) < 0.01) {
      y -= 6;
    }

    return { x: +x.toFixed(1), y: +y.toFixed(1) };
  }

  public labelAnchor(angle: number): string {
    const cos = Math.cos(angle);
    if (Math.abs(cos) < 0.15) return 'middle';
    return cos > 0 ? 'start' : 'end';
  }
}
