import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { JobFeedService } from '../job-feed/job-feed.service';
import { JobSearchResultDto, ExperienceLevel } from '../job-feed/job.model';
import { ThemeService } from '../theme.service';
import { NotificationService } from '../notifications/notification.service';

export interface SkillGapItem {
  skill: string;
  category: 'critical' | 'recommended';
  importance: number; // 1-100
  reason: string;
  added: boolean;
}

@Component({
  selector: 'app-cv-builder',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './cv-builder.component.html',
  styleUrls: ['./cv-builder.component.css']
})
export class CvBuilderComponent implements OnInit {
  private fb = inject(FormBuilder);
  private jobService = inject(JobFeedService);
  public themeService = inject(ThemeService);
  private notificationService = inject(NotificationService);

  // Available target jobs from feed
  targetJobs = signal<JobSearchResultDto[]>([]);
  selectedJobId = signal<string>('sample-job');

  // Candidate CV Profile
  cvForm: FormGroup;
  candidateSkills = signal<string[]>([
    '.NET',
    'C#',
    'PostgreSQL',
    'REST APIs',
    'Git'
  ]);

  newSkillInput = signal<string>('');

  // Target Job Details
  targetTitle = signal<string>('Senior Full-Stack .NET & Angular Engineer');
  targetCompany = signal<string>('FinTech Systems MENA');
  targetRequiredSkills = signal<string[]>([
    '.NET',
    'C#',
    'Angular',
    'TypeScript',
    'PostgreSQL',
    'Docker',
    'Kubernetes',
    'Microservices',
    'CI/CD'
  ]);

  // ATS Threshold Configuration
  atsThreshold = signal<number>(75);

  // Suggested popular skills to quickly add
  suggestedSkills = [
    'Angular',
    'TypeScript',
    'Docker',
    'Kubernetes',
    'Azure',
    'AWS',
    'Redis',
    'GraphQL',
    'Microservices',
    'RabbitMQ'
  ];

  constructor() {
    this.cvForm = this.fb.group({
      fullName: ['Ahmad Al-Mansoor', [Validators.required]],
      email: ['ahmad.mansoor@example.com', [Validators.required, Validators.email]],
      headline: ['Senior Backend & Cloud Solutions Developer', [Validators.required]],
      yearsOfExperience: [5, [Validators.required, Validators.min(0), Validators.max(40)]],
      summary: [
        'Experienced software engineer specializing in scalable backend services, distributed architectures, and relational database performance tuning. Proven track record in high-volume enterprise systems.'
      ]
    });
  }

  ngOnInit(): void {
    // Fetch live jobs to populate the target job selector
    this.jobService.searchJobs({ pageSize: 15 }).subscribe({
      next: (res) => {
        if (res.items.length > 0) {
          this.targetJobs.set(res.items);
        }
      },
      error: () => {
        // Fallback to sample job
      }
    });
  }

  // Handle Target Job selection
  onTargetJobSelected(jobId: string): void {
    this.selectedJobId.set(jobId);
    const job = this.targetJobs().find(j => j.id === jobId);
    if (job) {
      this.targetTitle.set(job.title);
      this.targetCompany.set(job.companyName);
      if (job.skills && job.skills.length > 0) {
        this.targetRequiredSkills.set(job.skills);
      } else {
        // Generate skills from title/context
        this.targetRequiredSkills.set(['.NET', 'TypeScript', 'PostgreSQL', 'Docker', 'Angular']);
      }
    }
  }

  // ATS Score Calculation (Authentic keyword match + experience fit)
  atsScore = computed(() => {
    const required = this.targetRequiredSkills();
    const candidate = this.candidateSkills();
    if (required.length === 0) return 100;

    const matched = required.filter(req =>
      candidate.some(cand => cand.toLowerCase().trim() === req.toLowerCase().trim())
    );

    const keywordRatio = matched.length / required.length; // 0 to 1
    const baseScore = keywordRatio * 85;

    // Experience bonus
    const expYears = Number(this.cvForm?.get('yearsOfExperience')?.value || 3);
    const expBonus = Math.min(15, expYears * 2.5);

    return Math.min(100, Math.round(baseScore + expBonus));
  });

  // Dynamic Skill Gap Analysis
  skillGaps = computed<SkillGapItem[]>(() => {
    const required = this.targetRequiredSkills();
    const candidate = this.candidateSkills();

    const missing = required.filter(req =>
      !candidate.some(cand => cand.toLowerCase().trim() === req.toLowerCase().trim())
    );

    return missing.map((skill, index) => {
      // Categorize: First 2 missing or foundational skills are Critical, others are Recommended
      const isCritical = index < 2;
      return {
        skill,
        category: isCritical ? 'critical' : 'recommended',
        importance: isCritical ? 95 - (index * 5) : 70 - (index * 5),
        reason: isCritical
          ? `Primary keyword required by ${this.targetCompany()}`
          : 'High-value differentiator that boosts ranking in ATS parsing',
        added: false
      };
    });
  });

  // Action: Add missing skill to candidate CV
  addSkillToCv(skillName: string): void {
    const trimmed = skillName.trim();
    if (!trimmed) return;

    if (!this.candidateSkills().some(s => s.toLowerCase() === trimmed.toLowerCase())) {
      this.candidateSkills.update(skills => [...skills, trimmed]);
      this.notificationService.show({
        type: 'success',
        title: 'Skill Added to CV',
        message: `Added "${trimmed}" to your skills profile. ATS Match score increased!`
      });
    }
  }

  // Action: Remove skill from CV
  removeSkillFromCv(skillName: string): void {
    this.candidateSkills.update(skills =>
      skills.filter(s => s.toLowerCase() !== skillName.toLowerCase())
    );
  }

  // Add custom skill via input
  addCustomSkill(): void {
    const val = this.newSkillInput().trim();
    if (val) {
      this.addSkillToCv(val);
      this.newSkillInput.set('');
    }
  }

  setThreshold(val: number): void {
    this.atsThreshold.set(val);
  }
}
