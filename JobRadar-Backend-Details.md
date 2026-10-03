# 🛰️ JobRadar — التوثيق الهندسي والمعماري الشامل للباك إند (Backend Architecture & Deep Dive)

> **الإصدار:** 1.0.0  
> **المنصة المستهدفة:** .NET 8 (C# 12)  
> **النمط المعماري:** Clean Architecture + CQRS + Event-Driven Workers + Modular Monolith  
> **قاعدة البيانات والبحث:** PostgreSQL 16 + pgvector (HNSW Indexing) + pg_trgm + Full-Text Search  
> **الذكاء الاصطناعي:** Google Gemini 3.7 Flash (Extraction) & Gemini Embedding 2 (1536-dim Vectors)  
> **التواصل اللحظي:** ASP.NET Core SignalR (Dynamic Relevance Groups)  
> **المراسلة والمهام المجدولة:** MassTransit (RabbitMQ / EF Core Outbox) + Hangfire Server  

---

## 📑 فهرس المحتويات

1. [نظرة عامة على المعمارية (High-Level Architecture)](#1-نظرة-عامة-على-المعمارية)
2. [هيكلية المشاريع والمسؤوليات (Project Structure)](#2-هيكلية-المشاريع-والمسؤوليات)
3. [طبقة النطاق (Domain Layer — `JobRadar.Domain`)](#3-طبقة-النطاق-domain-layer)
4. [طبقة التطبيق وحالات الاستخدام (Application Layer — `JobRadar.Application`)](#4-طبقة-التطبيق-application-layer)
5. [طبقة البنية التحتية والوصول للبيانات (Infrastructure Layer — `JobRadar.Infrastructure`)](#5-طبقة-البنية-التحتية-infrastructure-layer)
6. [محرك البحث الهجين الدلالي (Hybrid Semantic Search Engine)](#6-محرك-البحث-الهجين-الدلالي)
7. [خط أنابيب الزواحف وجلب البيانات (Crawlers & Ingestion Pipeline)](#7-خط-أنابيب-الزواحف-وجلب-البيانات)
8. [منظومة الذكاء الاصطناعي (AI & LLM Services)](#8-منظومة-الذكاء-الاصطناعي)
9. [طبقة واجهة برمجة التطبيقات (Presentation & API Layer — `JobRadar.Api`)](#9-طبقة-واجهة-برمجة-التطبيقات-api-layer)
10. [منظومة التواصل اللحظي (SignalR Real-Time Engine)](#10-منظومة-التواصل-اللحظي-signalr)
11. [خادم المعالجات الخلفية (Workers & Hangfire — `JobRadar.Workers`)](#11-خادم-المعالجات-الخلفية-workers)
12. [الأمان، المصادقة وتفويض الصلاحيات (Security, Identity & JWT)](#12-الأمان-والمصادقة)
13. [مخططات تدفق البيانات (End-to-End Workflows)](#13-مخططات-تدفق-البيانات)
14. [دليل الإعداد والتشغيل المحلي (Setup & Execution Guide)](#14-دليل-الإعداد-والتشغيل-المحلي)

---

## 1. نظرة عامة على المعمارية

تم بناء الباك إند الخاص بمشروع **JobRadar** بالاعتماد الصارم على مبادئ **Clean Architecture (Onion Architecture)** مع نمط **CQRS (Command Query Responsibility Segregation)** مستعيناً بمكتبة `MediatR`.

الهدف الأساسي هو جعل جوهر الأعمال (Core Business Rules) مستقلاً تماماً عن قواعد البيانات، ومكتبات واجهات المستخدم، ومزودي خدمات الذكاء الاصطناعي الخارجية، مما يمنح النظام مرونة فائقة وقابلية عالية للاختبار وتوسيع النطاق.

```mermaid
flowchart TB
    subgraph Clients["طبقة العميل (Clients)"]
        WEB["Angular 17 SPA"]
        MOB["Mobile App / Extension"]
    end

    subgraph Presentation["JobRadar.Api (Presentation Layer)"]
        API["ASP.NET Core Controllers"]
        HUB["SignalR Hub (/hubs/jobs)"]
        MID["Middlewares: CorrelationId, ProblemDetails, RateLimiting"]
    end

    subgraph Application["JobRadar.Application (Use Cases / CQRS)"]
        CMD["Commands (Create, Ingest, Apply, Auth)"]
        QRY["Queries (SearchJobs, GetDetails, Applications)"]
        VAL["FluentValidation Behaviors"]
        LOG["Logging Behaviors"]
    end

    subgraph Domain["JobRadar.Domain (Enterprise Core)"]
        ENT["Entities (Job, RawPost, Source, User, Cv)"]
        ENUM["Enums & Aggregate Roots"]
        COM["Common Base Classes & Exceptions"]
    end

    subgraph Infrastructure["JobRadar.Infrastructure (External Concerns)"]
        EF["EF Core 8 + Npgsql"]
        PGV["PostgreSQL + pgvector (1536 dims)"]
        GEM["Google Gemini API (Flash + Embeddings)"]
        CRW["Crawlers: Remotive, Arbeitnow, WWR, RSS, Telegram"]
        RED["Distributed Cache (Redis / Memory)"]
    end

    subgraph Workers["JobRadar.Workers (Asynchronous Operations)"]
        HANG["Hangfire Server (Queues: ingestion, cleanup, default)"]
        MT["MassTransit + RabbitMQ (Outbox Pattern)"]
        CHNL["System.Threading.Channels (Embedding Batching)"]
    end

    WEB & MOB --> MID --> API & HUB
    API --> Application
    Application --> Domain
    Infrastructure -.-> Application
    Infrastructure --> PGV & GEM & RED
    Workers --> Application & Infrastructure
```

---

## 2. هيكلية المشاريع والمسؤوليات

يحتوي مجلد `src` على 6 مشاريع رئيسية لتقسيم المسؤوليات:

| المشروع | نوع المشروع | المسؤولية والدور المعماري |
|---|---|---|
| **`JobRadar.Domain`** | Class Library | **نواة النظام الصافية**: لا يعتمد على أي مشروع آخر داخل الـ Solution. يحتوي على الكيانات (Entities)، التعدادات (Enums)، القواعد المشتركة، ولا يحتوي على أي كود يتعلق بقاعدة البيانات أو بروتوكولات الاتصال. |
| **`JobRadar.Application`** | Class Library | **منطق حالات الاستخدام (Use Cases)**: يعتمد فقط على `Domain`. يطبق نمط CQRS باستخدام MediatR، والتحقق عبر FluentValidation، ويحتوي على عقود الواجهات (Interfaces/Abstractions) للخدمات والمستودعات. |
| **`JobRadar.Infrastructure`** | Class Library | **تنفيذ البنية التحتية والتقنيات الخارجية**: يعتمد على `Application` و `Domain`. يضم سياق قاعدة البيانات `AppDbContext`، كائنات التكوين لـ EF Core، مستودعات البيانات (Repositories)، زواحف الويب (Crawlers)، تكامل Gemini AI، ومستهلكي MassTransit. |
| **`JobRadar.Api`** | Web API (.NET 8) | **نقطة الدخول والخدمات السحابية**: يحتوي على المتحكمات (Controllers)، وموزع SignalR Hub، وميدلوير معالجة الأخطاء والـ Correlation ID، وإعدادات المصادقة والتفويض بـ JWT، وإعدادات Rate Limiting، وتوثيق Swagger. |
| **`JobRadar.Workers`** | Console Worker Host | **معالج العمليات الخلفية المجدولة**: خادم Hangfire مخصص لتشغيل مهام سحب الوظائف دورياً، وحذف الوظائف القديمة، ومعالجة رسائل RabbitMQ بشكل منفصل عن خادم الـ API لمنع استهلاك موارد خادم الويب. |
| **`JobRadar.Tester`** | Console App | أداة سريعة لتجربة استدعاءات API والزواحف وفحص الاتصال بقواعد البيانات محلياً. |

---

## 3. طبقة النطاق (Domain Layer)

تقع في المشروع [`JobRadar.Domain`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain). تمثل مركز الثقل ولا تعتمد على أي مكتبات خارجية سوى نوع البيانات `Pgvector.Vector` لدعم تضمين المتجهات.

### 3.1 الكيانات الرئيسية (Entities)

#### 1. [`Job`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/Job.cs) (Aggregate Root)
يمثل الوظيفة بعد معالجتها وهيكلتها:
- **`Id`**: معرف فريد `Guid`.
- **`SourceId`**: ربط بالمصدر الذي تم استخراج الوظيفة منه (`Source`).
- **`RawPostId`**: ربط اختياري بالمنشور الخام (`RawPost`).
- **`Title`**, **`CompanyName`**, **`Description`**, **`Location`**: البيانات الأساسية للوظيفة.
- **`IsRemote`**: تحديد ما إذا كانت الوظيفة عن بُعد بالكامل.
- **`EmploymentType`**: نوع التوظيف (دوام كامل، جزئي، تدريب، عقد، حر...).
- **`ExperienceLevel`**: مستوى الخبرة (مبتدئ، متوسط، خبير، قائد فريق، مدير...).
- **`SalaryMin`**, **`SalaryMax`**, **`SalaryCurrency`**: نطاق الراتب والعملة.
- **`ExternalApplyUrl`**: رابط التقديم الخارجي المباشر.
- **`PostedAt`**, **`ExpiresAt`**, **`IsActive`**: تواريخ النشر والانتهاء وحالة التفعيل.
- **`ViewsCount`**, **`ApplicantsClickCount`**: عدادات التفاعل والنقرات.
- **`Embedding`**: متجه رقمي بطول **1536 بعداً** من نوع `Vector` يُخزن في PostgreSQL عبر إضافة `pgvector`، ويُنشأ له فهرس HNSW للبحث الدلالي اللحظي.
- **علاقات التصفح (Navigations):**
  - `IReadOnlyCollection<JobSkillMap> JobSkills`
  - `IReadOnlyCollection<UserSavedJob> SavedByUsers`
  - `IReadOnlyCollection<UserJobApplication> Applications`
  - `IReadOnlyCollection<CvJobMatchAnalysis> MatchAnalyses`

#### 2. [`RawPost`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/RawPost.cs)
يمثل النص الخام المجلوب من قنوات تيليجرام أو موجزات RSS قبل تحليله:
- `RawContent`: النص الأصلي كما ورد في المنشور.
- `RawUrl`: رابط المنشور الأصلي (يستخدم لمنع تكرار الجلب).
- `ProcessingStatus`: حالة المعالجة (`New`, `Processing`, `Processed`, `Rejected`).

#### 3. [`Source`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/Source.cs)
يمثل مصادر الجلب المتعددة:
- `Name`: اسم المصدر.
- `Type`: نوع المصدر (`TelegramChannel`, `RssFeed`, `CompanyCareersPage`, `ManualShare`).
- `Url`: الرابط أو معرف القناة.
- `Status`: حالة المصدر (`Active`, `Paused`, `Pending`, `Rejected`).
- `FetchIntervalMinutes`: الفاصل الزمني المخصص لجدولة الجلب.
- `LastSyncIdentifier`: مؤشر آخر منشور تمت قراءته لمنع التكرار.
- `ConsecutiveFailureCount`: عداد الفشل المتتابع (إذا وصل إلى 5 يتم إيقاف المصدر مؤقتاً تلقائياً للحماية).

#### 4. [`ApplicationUser`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/ApplicationUser.cs)
يمتد من `IdentityUser<Guid>`:
- `FullName`: الاسم الكامل.
- `PreferredJobTitles`: مصفوفة الكلمات المفتاحية للمسميات الوظيفية المفضلة.
- `PreferredLocations`: مواقع العمل المرغوبة.
- `PreferredSkills`: المهارات التقنية التي يتقنها المستخدم.
- `ExperienceLevel`: مستوى الخبرة الحالي.
- `TelegramChatId`: معرف حساب التيليجرام لإرسال التنبيهات الفورية عبر بوت تيليجرام.

#### 5. [`RefreshToken`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/RefreshToken.cs)
تأمين جلسات المستخدمين وتجديد رموز JWT:
- `TokenHash`: تشفير SHA-256 للرمز السري.
- `ExpiresAt`, `CreatedAt`, `CreatedByIp`.
- `RevokedAt`, `RevokedByIp`, `ReplacedByTokenHash`, `ReasonRevoked`.
- خصائص مساعدة مثل `IsActive`, `IsExpired`, `IsRevoked`.

#### 6. كيانات منظومة السيرة الذاتية (CV & ATS Matching)
- **[`Cv`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/Cv.cs)**: يحوي السيرة الذاتية بصيغة JSON مهيكلة تضم الخبرات والتعليم والمهارات والمشاريع، ورابط الـ PDF المولّد.
- **[`CvJobMatchAnalysis`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/CvJobMatchAnalysis.cs)**: نتيجة مقارنة السيرة الذاتية مع وظيفة محددة عبر الذكاء الاصطناعي، متضمنة `AtsScore (0-100)`، والكلمات المفتاحية الناقصة `MissingKeywords[]`، واقتراحات التحسين `Suggestions`.
- **[`CvTemplate`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/CvTemplate.cs)**: قوالب تصميم الـ PDF الجاهزة.

#### 7. الكيانات الوسيطة والمكملة
- **[`Skill`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/Skill.cs)** و **[`JobSkillMap`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/JobSkillMap.cs)**: جدول موحد للمهارات مع Slug فريد وربط متعدد بكيان `Job`.
- **[`UserJobApplication`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/UserJobApplication.cs)**: سجل تقديم المستخدم مع حالة الطلب وملاحظات المتابعة.
- **[`UserSavedJob`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/UserSavedJob.cs)**: المفضلة وحفظ الوظائف.
- **[`Notification`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Domain/Entities/Notification.cs)**: إشعارات النظام وتنبيهات الوظائف الجديدة عبر القنوات المختلفة (`InApp`, `Telegram`, `Email`).

---

## 4. طبقة التطبيق وحالات الاستخدام (Application Layer)

تعتمد هذه الطبقة على مكتبة `MediatR` لتطبيق مبدأ فصل الأوامر عن الاستعلامات (CQRS). لا تحتوي هذه الطبقة على أي كود يتعامل مباشرة مع SQL أو استدعاءات HTTP المباشرة؛ بل تعتمد على واجهات برمجية مجردة (Abstractions).

### 4.1 خط أنابيب MediatR (Pipeline Behaviors)
يتم تمرير كل طلب (Command أو Query) عبر سلوكيات وسيطة تلقائية مسجلة في [`DependencyInjection.cs`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/DependencyInjection.cs):
1. **[`LoggingBehavior<TRequest, TResponse>`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/Behaviors/LoggingBehavior.cs)**:
   يسجل دخول كل أمر/استعلام، ومعرفاته، ومدة التنفيذ بالمللي ثانية، وأي استثناءات تقع خلال المعالجة عبر هيكل سجلات Serilog الموحد.
2. **[`ValidationBehavior<TRequest, TResponse>`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/Behaviors/ValidationBehavior.cs)**:
   يقوم بفحص الطلب مقابل أي `AbstractValidator<T>` مكتوب له بواسطة `FluentValidation`. إذا فشلت أي قاعدة، يتم إيقاف الطلب فوراً وقذف `ValidationException` ليتحول في طبقة الـ API إلى استجابة `422 Unprocessable Entity` أو `400 Bad Request` بصيغة RFC 7807 المشروحة.

### 4.2 تفصيل الميزات وحالات الاستخدام (Features)

#### 1. ميزة المصادقة وإدارة الحسابات (`Features/Auth`)
- **`RegisterCommand`**: التحقق من فرادة البريد الإلكتروني، وإنشاء مستخدم جديد في Identity، ومنحه دور `User` الافتراضي، وإطلاق `UserRegisteredEvent`.
- **`LoginCommand`**: التحقق من البريد الإلكتروني وكلمة المرور، وإنشاء Access Token (JWT) صالح لمدة 60 دقيقة، وإنشاء Refresh Token مشفر وتخزينه في جدول `RefreshTokens`.
- **`RefreshTokenCommand`**: فحص صحة الـ Refresh Token المنتهي والتحقق من عدم إلغائه، وتدوير الرمز (Rotation) لمنع هجمات إعادة الاستخدام، وإصدار زوج توكنز جديد.
- **`LogoutCommand`**: إبطال الـ Refresh Token المرتبط وحذفه من الكوكيز.

#### 2. ميزة الوظائف وإدارتها (`Features/JobPostings`)
- **`CreateJobPostingCommand`**: مخصص للأدمن والـ HR لإضافة وظائف رسمية؛ يقوم بإنشاء الوظيفة وربطها بالمصدر اليدوي `ManualSourceId`، وحفظها، ومن ثم بث إشعار فوري لحظي عبر SignalR للمجموعات المطابقة لشروط الوظيفة.
- **`DeleteJobPostingCommand`**: مخصص للأدمن لحذف أو تعطيل وظيفة معينة.
- **`GetJobPostingsQuery`**: استعلام مرن مع تقسيم الصفحات وفلاتر بحث نصية.
- **`GetJobPostingByIdQuery`**: جلب تفاصيل وظيفة كاملة بمهاراتها وشركتها.

#### 3. ميزة البحث المتقدم عن الوظائف (`Features/JobSearch`)
- **[`SearchJobsQuery`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/Features/JobSearch/Queries/SearchJobsQuery.cs)**:
  القلب النابض للنظام؛ يتيح استعلامات دلالية وهيكلية معقدة مع كاش هجين ثنائي الطبقات (تفاصيله موضحة في القسم 6).

#### 4. ميزة التقديم على الوظائف (`Features/JobApplications`)
- **`ApplyForJobCommand`**: يتيح للمستخدم المسجل التقديم على وظيفة معينة مع كتابة ملاحظات خاصة، وتحديث عداد النقرات للوظيفة.
- **`GetAllApplicationsQuery`**: استعلام خاص بمسؤولي التوظيف والمدراء لاستعراض المتقدمين وفلترتهم بحسب الوظيفة.

#### 5. ميزة جلب المصادر (`Features/Sources`)
- **`IngestSourceCommand`**: يتم تشغيله بواسطة المعالجات الخلفية أو الأدمن لجلب المحتوى من مصدر معين (`RssFetcher` أو `TelegramFetcher`) وتحويله إلى منشورات خام `RawPost` وإرسال أحداث للمستهلكين عبر MassTransit.

---

## 5. طبقة البنية التحتية (Infrastructure Layer)

تقوم طبقة [`JobRadar.Infrastructure`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure) بربط النظام بالعالم الخارجي ومزودي الخدمات.

### 5.1 سياق قاعدة البيانات وتكوينات الكيانات (`AppDbContext`)
ترث من `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` ومسجلة لدعم امتداد المتجهات:
```csharp
builder.HasPostgresExtension("vector");
```
وتوفر جداول:
- `Jobs`, `RawPosts`, `Sources`, `Skills`, `JobSkillMaps`
- `RefreshTokens`, `UserSavedJobs`, `UserJobApplications`, `Notifications`
- `CvTemplates`, `Cvs`, `CvJobMatchAnalyses`
- وجداول MassTransit الخاصة بنمط صندوق الإرسال (`AddInboxStateEntity`, `AddOutboxMessageEntity`, `AddOutboxStateEntity`).

### 5.2 الفهارس المتقدمة في PostgreSQL (Database Performance Indexing)
يقوم [`IdentityDataSeeder.cs`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/Persistence/IdentityDataSeeder.cs) بتطبيق فهارس متقدمة تلقائياً عند إقلاع التطبيق:
1. **فهرس HNSW للمتجهات**:
   ```sql
   CREATE INDEX IF NOT EXISTS ix_jobs_embedding_hnsw 
   ON jobs USING hnsw (embedding vector_cosine_ops);
   ```
2. **فهارس الثلاثيات النصية (Trigram Indexes عبر pg_trgm)**:
   ```sql
   CREATE EXTENSION IF NOT EXISTS pg_trgm;
   CREATE INDEX IF NOT EXISTS ix_jobs_trgm_title ON jobs USING gin (title gin_trgm_ops);
   CREATE INDEX IF NOT EXISTS ix_jobs_trgm_company ON jobs USING gin (company_name gin_trgm_ops);
   CREATE INDEX IF NOT EXISTS ix_jobs_trgm_location ON jobs USING gin (location gin_trgm_ops);
   ```
3. **فهرس البحث النصي الكامل المركب (Full-Text Search Index)**:
   ```sql
   CREATE INDEX IF NOT EXISTS ix_jobs_fts ON jobs USING gin (
       to_tsvector('english', title || ' ' || company_name || ' ' || coalesce(location, '') || ' ' || description)
   );
   ```
4. **فهارس الترتيب والفلترة الشائعة**:
   ```sql
   CREATE INDEX IF NOT EXISTS ix_jobs_active_posted_desc ON jobs (is_active, posted_at DESC);
   CREATE INDEX IF NOT EXISTS ix_jobs_active_salary ON jobs (is_active, salary_min, salary_max);
   ```

---

## 6. محرك البحث الهجين الدلالي (Hybrid Semantic Search Engine)

يعد محرك البحث في [`SearchJobsQueryHandler`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/Features/JobSearch/Queries/SearchJobsQuery.cs) و [`JobRepository`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/Persistence/Repositories/JobRepository.cs) أحد أهم نقاط القوة الهندسية في JobRadar. فهو يجمع بين **البحث الدلالي بالذكاء الاصطناعي (Semantic Vector Search)** و **الفلترة الهيكلية الصارمة (Structured Filtering)** و **البحث النصي (Trigram/FTS)** في خط أنابيب متكامل:

```mermaid
flowchart TD
    REQ[استلام SearchJobsQuery] --> HASH[حساب SHA-256 لكافة معايير الاستعلام كـ Canonical Key]
    HASH --> CACHE_CHK{هل النتائج موجودة في الكاش؟}
    CACHE_CHK -- نعم --> RET_CACHE[إرجاع النتائج فوراً بـ زمن < 2ms]
    CACHE_CHK -- لا --> EVAL_SEM{هل الاستعلام نصي والترتيب حسب الصلة Relevance؟}
    
    EVAL_SEM -- نعم --> EMB_CHK{هل تضمين النص موجود في كاش التضمينات؟}
    EMB_CHK -- نعم --> FETCH_EMB[جلب المتجه من الكاش]
    EMB_CHK -- لا --> GEMINI_EMB[استدعاء Gemini Embeddings API لتوليد 1536 أبعاد]
    GEMINI_EMB --> SAVE_EMB[حفظ المتجه في الكاش لمدة ساعة]
    FETCH_EMB & SAVE_EMB --> REPO_SEARCH
    
    EVAL_SEM -- لا --> FAST_PATH[استخدام المسار السريع Fast-Path بالفلترة المباشرة]
    FAST_PATH --> REPO_SEARCH
    
    subgraph DatabaseExecution["تنفيذ الاستعلام في PostgreSQL عبر EF Core"]
        REPO_SEARCH[تطبيق فلاتر: العمل عن بُعد، الموقع، نوع التوظيف، مستوى الخبرة، الراتب، المهارات، تاريخ النشر]
        REPO_SEARCH --> SUB_KW[دمج الفلترة بـ ILike / Trigram]
        SUB_KW --> COUNT[حساب TotalCount للصفحات]
        COUNT --> RANKING{هل المتجه موجود؟}
        RANKING -- نعم --> COSINE[حساب Cosine Distance عبر pgvector والترتيب تصاعدياً حسب المسافة]
        RANKING -- لا --> SORT_STD[الترتيب حسب التاريخ الأحدث أو الراتب الأكبر]
    end
    
    COSINE & SORT_STD --> DTO_MAP[تحويل الكيانات إلى JobSearchResultDto وحساب نسبة التطابق 0.0 - 1.0]
    DTO_MAP --> CACHE_SAVE[حفظ النتيجة في الكاش لمدة 5 دقائق]
    CACHE_SAVE --> RES[إرجاع PagedResult للمستخدم]
```

### 6.1 ميزات محرك البحث
1. **الكاش الدلالي المزدوج (Two-Tier Distributed Cache)**:
   - **Tier 1 (Results Cache):** كاش للنتائج النهائية مفهرس بـ Hash لجميع المعايير بما فيها الصفحة والترتيب. تنتهي صلاحيته بعد 5 دقائق.
   - **Tier 2 (Embedding Cache):** كاش للمتجهات الناتجة عن النصوص الطبيعية لمنع استدعاء Gemini لنفس العبارة مراراً وتكراراً. تنتهي صلاحيته بعد ساعة كاملة.
2. **المسار السريع (Fast-Path Optimization)**:
   إذا اختار المستخدم الترتيب حسب التاريخ الأحدث (`Newest`) أو الراتب الأعلى (`SalaryDescending`) دون وجود نص طبيعي، يتم تخطي استدعاء نموذج التضمينات كلياً والاستعلام مباشرة من الفهارس العادية لقاعدة البيانات في أجزاء من المللي ثانية.
3. **التسامح مع الأعطال (Graceful Fallback)**:
   إذا فشل الاتصال بخدمة Gemini أو انتهت الحصة المخصصة، يسجل النظام تحذيراً ويتراجع تلقائياً (Fallback) إلى البحث النصي الثلاثي (Trigram) والبحث النصي الكامل (FTS) دون أن يشعر المستخدم بأي خطأ أو توقف.

---

## 7. خط أنابيب الزواحف وجلب البيانات (Crawlers & Ingestion Pipeline)

تم تزويد الباك إند بنظام تجميع وظائف آلي متعدد المزودين يعمل في الخلفية ومصمم لتجنب التكرار وضمان حداثة البيانات.

### 7.1 مزودو الزواحف المدمجون (`IJobCrawlerProvider`)
1. **[`RemotiveCrawlerProvider`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/Crawlers/RemotiveCrawlerProvider.cs)**:
   - يتصل بـ Remotive API لجلب أحدث الوظائف البرمجية عن بُعد.
   - يقوم بتنظيف وصف الوظيفة من وسوم HTML ورموز الـ Entities.
   - يستنتج مستوى الخبرة (`ExperienceLevel`) ونوع التوظيف (`EmploymentType`) ويستخرج الرواتب والوسوم.
2. **[`ArbeitnowCrawlerProvider`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/Crawlers/ArbeitnowCrawlerProvider.cs)**:
   - يسحب وظائف المطورين والمهندسين من واجهة Arbeitnow API للوظائف العالمية والأوروبية وعن بُعد.
3. **[`WeWorkRemotelyRssCrawlerProvider`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/Crawlers/WeWorkRemotelyRssCrawlerProvider.cs)**:
   - يقرأ موجز RSS الخاص بمنصة WeWorkRemotely ويحلل نصوص الـ XML ويحولها لكائنات `DiscoveredJobDto`.

### 7.2 أمر التجميع وتنسيق البيانات (`IngestCrawledJobsCommand`)
تم نقل منطق التنسيق بالكامل إلى طبقة الـ Application تماشياً مع معمارية Clean Architecture عبر [`IngestCrawledJobsCommandHandler.cs`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/Features/Crawlers/Commands/IngestCrawledJobs/IngestCrawledJobsCommandHandler.cs):
1. **إنشاء المصدر التلقائي (System Source Provisioning)**: فحص واعتماد مصدر خاص بكل زاحف في جدول `Sources` تلقائياً عبر `ISourceRepository`.
2. **منع التكرار الصارم على مرحلتين (Two-Phase Deduplication)**:
   - **المرحلة الأولى:** استعلام دفعي عن الروابط `GetExistingUrlsAsync` لتصفية الروابط المخزنة مسبقاً في قاعدة البيانات أو داخل الدفعة الحالية.
   - **المرحلة الثانية:** فحص تطابق اسم الوظيفة واسم الشركة `ExistsByTitleAndCompanyAsync` لتلافي التكرار في حال تغيرت معايير الرابط.
3. **تسجيل المهارات ومطابقتها (Upsert Skills)**:
   استدعاء `IJobRepository.AddWithSkillsAsync` لمطابقة وإدراج المهارات الجديدة في جدول `Skills` وربطها بالوظيفة عبر `JobSkillMap`.
4. **طابور التضمين الدفعي (Batch Embedding Queue)**:
   إدراج معرّف الوظيفة `job.Id` في طابور `IJobEmbeddingQueue` ليتم معالجة متجهات التضمين في الخلفية دون استهلاك موارد المعالجة الفورية.
5. **البث اللحظي (SignalR Real-Time Broadcast)**:
   إشعار المستخدمين فور حفظ الوظيفة عبر تجزئة `IJobRealtimeNotifier` إلى مجموعات الاهتمام المطابقة.

### 7.3 الجدولة الدورية عبر Hangfire (`ExternalJobCrawlDispatcherJob`)
تم سحب الجدولة بالكامل من خادم الـ API وحصرها في مشروع `JobRadar.Workers`:
- وظيفة Hangfire المكررة [`ExternalJobCrawlDispatcherJob`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Workers/Jobs/ExternalJobCrawlDispatcherJob.cs) بجدولة دورية تقرأ من إعداد `JobCrawler:IntervalMinutes`.
- مزودة بالسمة `[DisableConcurrentExecution(timeoutInSeconds: 1800)]` والتي تعتمد على أقفال PostgreSQL التوزيعية لضمان عدم تشغيل دورتي زحف في آن واحد حتى لو تم تشغيل عدة نسخ متوازية (Replicas) من مشروع `Workers`.
- لم يعد تطبيق الـ API يستهلك أي موارد تشغيلية لخلفيات المعالجة أو الجدولة الدورية.

---

## 8. منظومة الذكاء الاصطناعي (AI & LLM Services)

تعتمد المنظومة على نماذج **Google Gemini** الحديثة وتُدار استدعاءاتها عبر خطوط أنابيب المرونة من مكتبة **Polly** (إعادة المحاولة مع التراجع الأسي وقاطع الدائرة Circuit Breaker).

### 8.1 استخراج البيانات المهيكلة (`GeminiExtractionService`)
- **النموذج المستخدم:** `gemini-3.7-flash` (سريع واقتصادي وعالي الدقة).
- **الآلية:** استخدام ميزة `responseSchema` في Gemini REST API لإلزام النموذج بإعادة كائن JSON يطابق المخطط بدقة تامة دون أي علامات Markdown أو نصوص جانبية:
  ```json
  {
    "type": "object",
    "properties": {
      "is_job_posting":   { "type": "boolean" },
      "confidence":       { "type": "number"  },
      "title":            { "type": "string"  },
      "company_name":     { "type": "string"  },
      "location":         { "type": "string"  },
      "is_remote":        { "type": "boolean" },
      "employment_type":  { "type": "string", "enum": ["FullTime","PartTime","Freelance","Internship","Contract","Temporary"] },
      "experience_level": { "type": "string", "enum": ["Internship","EntryLevel","MidLevel","Senior","Lead","Manager","Director","Executive"] },
      "skills_required":  { "type": "array", "items": { "type": "string" } },
      "salary_min":       { "type": "number"  },
      "salary_max":       { "type": "number"  },
      "salary_currency":  { "type": "string"  },
      "external_apply_url": { "type": "string" }
    },
    "required": ["is_job_posting","confidence","title","company_name","is_remote","employment_type","experience_level","skills_required"]
  }
  ```
- **الفلترة الذكية:** إذا كانت نسبة الثقة `confidence < 0.60` أو كان المنشور لا يمثل إعلان توظيف حقيقي (مجرد إعلان ترويجي أو استفسار على تيليجرام)، يقوم النموذج بإرجاع `is_job_posting: false` ليتم رفض المنشور تلقائياً وتغيير حالته إلى `Rejected`.
- **المرونة (Polly Pipeline):** 
  - إعادة المحاولة 3 مرات مع تراجع أسي (Exponential Backoff) يبدأ من ثانيتين.
  - قاطع دائرة (Circuit Breaker) ينفصل لمدة 30 ثانية إذا بلغت نسبة الفشل 50% لمنع إرهاق الواجهة.

### 8.2 توليد المتجهات الدلالية (`GeminiEmbeddingService`)
- **النموذج المستخدم:** `gemini-embedding-2`.
- **الأبعاد:** مُحددة بـ `output_dimensionality: 1536` لتطابق حقل `vector(1536)` في قاعدة البيانات وحجم المؤشرات القياسية.
- **تنسيق الاسترجاع:** استخدام صيغة `title: none | text: {text}` المتوافقة مع معايير Google Asymmetric Retrieval للبحث واسترجاع الوثائق.

---

## 9. طبقة واجهة برمجة التطبيقات (API Layer)

تمثل مشروع [`JobRadar.Api`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Api).

### 9.1 جدول نقاط النهاية (API Endpoints Map)

#### متحكم المصادقة (`/api/auth`)
| الدالة | المسار | الصلاحية | الوصف |
|---|---|---|---|
| `POST` | `/api/auth/register` | للجميع | تسجيل حساب جديد لمستخدم عادي. |
| `POST` | `/api/auth/login` | للجميع | تسجيل الدخول، استلام JWT في الرد وتعيين Refresh Token في كوكيز آمنة `HttpOnly`. |
| `POST` | `/api/auth/refresh-token` | للجميع | تجديد الـ Access Token المنتهي باستخدام Refresh Token صالح وتدويره. |
| `POST` | `/api/auth/logout` | للجميع | تسجيل الخروج وإلغاء صلاحية الـ Refresh Token في قاعدة البيانات ومسح الكوكي. |

#### متحكم الوظائف والبحث (`/api/jobs`)
| الدالة | المسار | الصلاحية | الوصف |
|---|---|---|---|
| `POST` | `/api/jobs/sync-now` | للجميع | تشغيل دورة زحف وجلب وظائف فورية واسترجاع عدد الوظائف الجديدة المكتشفة. |
| `POST` | `/api/jobs/search` | للجميع *(خاضع لـ Rate Limiting)* | البحث الهجين المتقدم عن الوظائف بجميع الفلاتر الهيكلية والدلالية. |
| `POST` | `/api/jobs/{id}/apply` | مسجل دخول `[Authorize]` | تسجيل طلب تقديم على الوظيفة باسم المستخدم الحالي وملاحظاته. |

#### متحكم إدارة الوظائف (`/api/job-postings`)
| الدالة | المسار | الصلاحية | الوصف |
|---|---|---|---|
| `GET` | `/api/job-postings` | للجميع | استعراض الوظائف بصفحات وتقسيم مرن وفلتر نصي عام. |
| `GET` | `/api/job-postings/{id}` | للجميع | استرجاع تفاصيل وظيفة كاملة بمهاراتها وشركتها. |
| `POST` | `/api/job-postings` | دور `Admin` أو `HR` | إنشاء ونشر وظيفة رسمية جديدة مع إطلاق تنبيه SignalR لحظي للمهتمين. |
| `DELETE`| `/api/job-postings/{id}` | دور `Admin` فقط | حذف وظيفة من النظام برقمها التعريفي. |

#### متحكم طلبات التوظيف (`/api/applications`)
| الدالة | المسار | الصلاحية | الوصف |
|---|---|---|---|
| `GET` | `/api/applications` | دور `Admin` أو `HR` | استعراض طلبات التقديم الخاصة بجميع الوظائف أو وظيفة معينة مع بيانات المتقدمين. |

### 9.2 الوسائط البرمجية المخصصة (Custom Middlewares)
مرتبة في خط أنابيب الطلب في [`Program.cs`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Api/Program.cs):
1. **[`CorrelationIdMiddleware`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Api/Middleware/CorrelationIdMiddleware.cs)**:
   - يقرأ رأس `X-Correlation-ID` من الطلب، أو يولد معرفاً جديداً إذا لم يوجد.
   - يضيفه إلى رأس الاستجابة وإلى سياق سجلات Serilog (`LogContext`) لربط كل سجل يخص الطلب عبر كافة الطبقات والخدمات.
2. **[`GlobalExceptionMiddleware`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Api/Middleware/GlobalExceptionMiddleware.cs)**:
   - يلتقط أي استثناء غير معالج.
   - يحول `FluentValidation.ValidationException` إلى استجابة خطأ `422 Unprocessable Entity` مع تفاصيل الحقول غير الصالحة.
   - يحول أخطاء `KeyNotFoundException` إلى `404 Not Found`.
   - يحول الاستثناءات العامة غير المتوقعة إلى `500 Internal Server Error` بصيغة `ProblemDetails` القياسية مع إخفاء تفاصيل السيرفر الداخلية عن العميل.
3. **محدد المعدل (Rate Limiting Middleware)**:
   - مطبق على نقطة نهاية البحث `/api/jobs/search` بسياسة نافذة ثابتة (Fixed Window):
     - الحد الأقصى: 20 استدعاء لكل دقيقة.
     - طابور الانتظار: 5 طلبات.
     - رمز الرفض: `429 Too Many Requests`.

---

## 10. منظومة التواصل اللحظي (SignalR)

بدلاً من إرسال تنبيهات عشوائية لجميع المتصلين عند نشر أي وظيفة (مما يسبب إزعاجاً وهدراً للباندويث)، يقدم JobRadar نظام **مجموعات الملاءمة اللحظية (Dynamic Relevance Groups)**:

```mermaid
sequenceDiagram
    autonumber
    actor User as العميل (Frontend)
    participant Hub as JobHub (/hubs/jobs)
    participant Crawler as الزاحف / الأدمن
    participant Notifier as SignalRJobRealtimeNotifier

    User->>Hub: اتصال SignalR
    User->>Hub: UpdateCriteriaSubscription(oldGroups, newGroups)
    Note over User,Hub: الاشتراك في مجموعات مثل:<br/>"remote:true", "exp:senior", "emp:fulltime", "skill:dotnet"
    
    Crawler->>Notifier: حفظ وظيفة جديدة (.NET Senior Remote)
    Notifier->>Notifier: حساب المجموعات المطابقة لهذه الوظيفة
    Notifier->>Hub: إرسال الحدث ReceiveRelevantJob فقط للمجموعات المطابقة
    Hub-->>User: وصول الوظيفة فوراً على واجهة العميل المتطابق مع الشروط
```

### 10.1 منطق المجموعات في [`JobRelevanceGroups`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Application/Common/JobRelevanceGroups.cs)
تتولد مفاتيح المجموعات وفق معايير دقيقة:
- `all:jobs`: لجميع المتصلين الراغبين بمتابعة كل ما يستجد.
- `remote:{true/false}`: حسب تفضيل العمل عن بُعد.
- `emp:{(int)EmploymentType}`: حسب نوع العقد.
- `exp:{(int)ExperienceLevel}`: حسب مستوى الخبرة.
- `loc:{clean_location}`: للمواقع الجغرافية المحددة.
- `skill:{clean_skill_name}`: للمهارات المطلوبة في الوظيفة.

---

## 11. خادم المعالجات الخلفية (Workers)

يتم تشغيل مشروع [`JobRadar.Workers`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Workers) كعملية منفصلة (Background Worker Host) لإنجاز الأعمال الثقيلة دون إبطاء خادم الـ API.

### 11.1 خادم Hangfire
- يستخدم قاعدة بيانات PostgreSQL وجدولاً مخصصاً باسم `hangfire` لحفظ حالات المهام ومواعيدها وسجلاتها.
- يعمل بعدد عمال `WorkerCount = Environment.ProcessorCount * 2`.
- يدير ثلاث طوابير ذات أولوية:
  1. `ingestion`: لمهام جلب ومعالجة مصادر البيانات.
  2. `cleanup`: لمهام التنظيف والأرشفة.
  3. `default`: لأي مهام أخرى في النظام.
- **المهام المتكررة (Recurring Jobs):**
  - **`ingestion-dispatcher`**: يعمل كل دقيقة (`Cron.Minutely()`) لفحص المصادر النشطة في جدول `Sources` التي حان وقت جلبها وجدولتها للتنفيذ.
  - **`stale-job-cleanup`**: يعمل كل يوم عند منتصف الليل بتوقيت UTC (`Cron.Daily()`) لأرشفة وتعطيل الوظائف التي تجاوزت مدة صلاحيتها أو التي لم تعد متاحة.

### 11.2 طابور توليد المتجهات الدفعي (`EmbeddingBatchProcessor`)
يوجد في [`EmbeddingBatchProcessor.cs`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/BackgroundServices/EmbeddingBatchProcessor.cs):
- يعتمد على `System.Threading.Channels` بسعة 2000 عنصر.
- يسمح بإرسال معرفات الوظائف إليه دون انتظار (`Non-blocking`).
- يجمع المعرفات في دفعات تصل إلى 10 وظائف دفعة واحدة (`BatchSize = 10`)، ثم يولد لها المتجهات عبر `GeminiEmbeddingService` ويحفظها في قاعدة البيانات، مما يقلل عدد الاتصالات بقاعدة البيانات ويسرع استهلاك الرسائل.

### 11.3 معالجة الرسائل ونمط صندوق الإرسال (MassTransit Outbox & RabbitMQ)
- يستمع [`RawPostProcessingConsumer`](file:///c:/Users/Dell/.gemini/antigravity-ide/scratch/JobRadar/src/JobRadar.Infrastructure/Consumers/RawPostProcessingConsumer.cs) لرسائل `RawPostCreatedEvent` القادمة عبر طابور `raw-post-processing`.
- يحدد معدل المعالجة المتزامنة بـ 4 رسائل فقط (`ConcurrentMessageLimit = 4`) لتجنب تجاوز حدود استدعاءات API الخاصة بنموذج Gemini.
- يطبق نمط **EF Core Outbox** لضمان عدم فقدان أي حدث عند وقوع أخطاء في الشبكة بين قاعدة البيانات و RabbitMQ.

---

## 12. الأمان والمصادقة

### 12.1 الهوية والمستخدمين (ASP.NET Core Identity)
- النظام مجهز بـ `IdentityCore<ApplicationUser>` مع الأدوار `AddRoles<ApplicationRole>()`.
- شروط كلمات المرور مشددة: أرقام، أحرف صغيرة، أحرف كبيرة، رموز خاصة، وطول لا يقل عن 6 خانات.
- الأدوار الأساسية المعتمدة في النظام:
  - **`Admin`**: الصلاحية الكاملة (إدارة المصادر، إضافة/حذف الوظائف، مراجعة كافة الطلبات).
  - **`HR`**: صلاحيات إضافة الوظائف ومراجعة طلبات التقديم.
  - **`User`**: البحث، التقديم، المفضلة، وإدارة السيرة الذاتية.

### 12.2 تشفير وتدوير الرموز (JWT & Refresh Token Rotation)
- يتم توقيع Access Token باستخدام مفتاح سري قوي (HMAC-SHA256) مع التحقق من الـ Issuer و Audience وفترة الصلاحية بدون تهاون في التوقيت (`ClockSkew = TimeSpan.Zero`).
- يتم تخزين الـ Refresh Token في قاعدة البيانات مشفراً بنظام الـ Hash مع تخزين عنوان IP المنشئ والمسترجع.
- يُرسل الـ Refresh Token إلى المتصفح عبر كوكي `HttpOnly; Secure; SameSite=Strict` لمنع هجمات XSS وسرقة الجلسات.

---

## 13. مخططات تدفق البيانات

### 13.1 دورة حياة المنشور من التيليجرام أو RSS إلى وظيفة معتمدة
```mermaid
sequenceDiagram
    autonumber
    participant HANG as Hangfire (IngestionDispatcherJob)
    participant FET as Fetcher (Telegram / RSS)
    participant DB as AppDbContext
    participant BUS as RabbitMQ / MassTransit Outbox
    participant CONS as RawPostProcessingConsumer
    participant AI as Gemini 3.7 Flash
    participant EMB as EmbeddingBatchProcessor
    participant SIG as SignalR Hub

    HANG->>FET: فحص المصادر المستحقة للجلب
    FET->>FET: جلب المنشورات الجديدة
    FET->>DB: حفظ المنشور كـ RawPost (Status: New)
    DB->>BUS: نشر حدث RawPostCreatedEvent عبر Outbox
    BUS->>CONS: استلام الرسالة في طابور raw-post-processing
    CONS->>AI: استخراج البيانات المهيكلة بالـ JSON Schema
    alt المحتوى ليس وظيفة أو الثقة < 0.60
        CONS->>DB: تحديث RawPost (Status: Rejected)
    else الوظيفة صالحة ومؤكدة
        CONS->>DB: إنشاء كيان Job مهيكل وحفظ المهارات
        CONS->>EMB: كتابة JobId في قناة الـ Embedding Channel
        EMB->>DB: توليد المتجه وحفظ Vector(1536)
        CONS->>SIG: إرسال تنبيه للمستخدمين المتصلين بالمجموعات المطابقة
    end
```

---

## 14. دليل الإعداد والتشغيل المحلي

### 14.1 المتطلبات المسبقة
- **.NET 8 SDK** (الإصدار 8.0 فما فوق).
- **Docker Desktop** (لتشغيل PostgreSQL مع إضافة pgvector، و RabbitMQ).
- **مفتاح Google Gemini API Key** من [Google AI Studio](https://aistudio.google.com/).

### 14.2 خطوات التشغيل

#### 1. تشغيل الخدمات التابعة عبر Docker Compose
من المجلد الرئيسي للمشروع:
```bash
docker compose up -d
```
سيقوم بتشغيل:
- حاوية `jobradar_db` (PostgreSQL 16 مع pgvector) على المنفذ `5432`.
- حاوية `jobradar_rabbitmq` على المنفذ `5672` ولوحة الإدارة على المنفذ `15672` (المستخدم/كلمة المرور: `guest`/`guest`).

#### 2. ضبط أسرار التطبيق (User Secrets / Configuration)
في مجلد `src/JobRadar.Api`:
```bash
cd src/JobRadar.Api
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=localhost;Port=5432;Database=jobradar;Username=postgres;Password=password"
dotnet user-secrets set "Gemini:ApiKey" "AIzaSyYourGeminiApiKeyHere..."
```

وفي مجلد `src/JobRadar.Workers`:
```bash
cd ../JobRadar.Workers
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=localhost;Port=5432;Database=jobradar;Username=postgres;Password=password"
dotnet user-secrets set "ConnectionStrings:RabbitMQ" "amqp://guest:guest@localhost:5672"
dotnet user-secrets set "Gemini:ApiKey" "AIzaSyYourGeminiApiKeyHere..."
```

#### 3. تطبيق تهجيرات قاعدة البيانات (Database Migrations)
من المجلد الرئيسي:
```bash
dotnet ef database update --project src/JobRadar.Infrastructure --startup-project src/JobRadar.Api
```

#### 4. تشغيل خادم الـ API
```bash
dotnet run --project src/JobRadar.Api
```
- واجهة Swagger التفاعلية: `http://localhost:5000/swagger` أو `https://localhost:5001/swagger`
- مسار SignalR Hub اللحظي: `http://localhost:5000/hubs/jobs`

#### 5. تشغيل خادم المعالجات والمهام الخلفية (Workers)
في نافذة طرفية منفصلة:
```bash
dotnet run --project src/JobRadar.Workers
```
- لوحة تحكم Hangfire للمهام الخلفية: مفعلة على مسار الخادم الداخلي لمراقبة الطوابير.

#### 6. بيانات تسجيل الدخول الافتراضية للمسؤول (Default Admin)
يتم إعدادها عبر User Secrets / Environment Variables فقط في الـ Seeder:
- **البريد الإلكتروني:** `<SET_VIA_USER_SECRETS>` (مفتاح: `AdminSeed:Email`)
- **كلمة المرور:** `<SET_VIA_USER_SECRETS>` (مفتاح: `AdminSeed:Password`)
- **الدور:** `Admin`

---

## 🏁 خلاصة التقييم المعماري

يتميز الباك إند الخاص بـ **JobRadar** بالخصائص التالية:
- **معمارية هندسية منضبطة:** التزام تام بـ Clean Architecture و CQRS بدون تسريب تفاصيل البنية التحتية إلى النواة.
- **بحث دلالي ثوري:** الاعتماد على pgvector ومسافات جيب التمام مع كاش ذكي ثنائي الطبقات ومسار سريع لتقليل زمن الاستجابة إلى أجزاء من الثانية.
- **استخراج بيانات ذكي ومرن:** توظيف نماذج Gemini Flash مع الـ JSON Schemas ونمط Polly لضمان عدم توقف النظام عند تقلبات الشبكة.
- **تواصل فوري محدد الأولويات:** استبدال البث العشوائي بـ Relevance Groups عبر SignalR.
- **جاهزية الإنتاج:** تطبيق Outbox Pattern، وفهارس GIN/Trigram/HNSW، ومحددات المعدل، وسجلات Serilog المرتبطة بـ Correlation IDs.
