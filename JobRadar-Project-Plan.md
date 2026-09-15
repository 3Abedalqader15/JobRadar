# JobRadar — منصة تجميع الوظائف اللحظية من مواقع التواصل الاجتماعي

> اسم مقترح للمشروع: **JobRadar** (بإمكانك تغييره لاحقًا). ملف تخطيط شامل: الفكرة، الـERD، الـ UI/UX، الـ Tech Stack، مراحل التنفيذ، بدائل موفرة للتكلفة، و Prompts جاهزة لأداة Antigravity.

---

## 1. الفكرة بالمختصر

منصة تجمع كل الوظائف المنشورة على منصات التواصل الاجتماعي (LinkedIn, Facebook Groups, Twitter/X, Telegram Channels, WhatsApp Channels لاحقًا) وكمان مواقع توظيف عامة، بشكل **لحظي (Real-time / Near real-time)**، وتعرضها للمستخدم بطريقة مبحوثة ومفلترة وذكية، مع إمكانية:

- المستخدم يضيف "مصدر" (صفحة/قروب/قناة/موقع) بنفسه عشان يتم مراقبته تلقائيًا.
- بحث ذكي (Semantic Search عن طريق RAG) مو بس Keyword matching.
- بناء CV بنظام ATS احترافي مرتبط مباشرة بالوظائف المعروضة (اقتراح تعديلات على الـCV حسب الوظيفة).
- نظام إشعارات لحظي (Push / Email / Telegram Bot) لما توظيفة تطابق بروفايل المستخدم.

### القيمة التنافسية (Unique Selling Points)
1. **تغطية مصادر غير تقليدية**: جروبات فيسبوك وقنوات تيليجرام غالبًا ما يفوتها LinkedIn/Bayt/Indeed.
2. **مصادر يضيفها المستخدم نفسه** — كل مستخدم يقدر "يغذي" النظام بمصادر جديدة (Crowdsourced Sources).
3. **RAG-powered matching** بدل الـ keyword search التقليدي.
4. **ATS CV Builder** متكامل مع نتائج البحث — مو أداة منفصلة.

---

## 2. تحدي أساسي لازم تكون واعي له من البداية

جمع بيانات من فيسبوك/لينكدإن/إنستغرام آليًا (Scraping) **يخالف Terms of Service** لهالمنصات ومعرض الحساب/الـIP للحظر، وبعض الدول عندها قوانين صارمة حول الـ Web Scraping (خصوصًا LinkedIn اللي عنده حماية قانونية فعّالة ودعاوى سابقة ضد شركات scraping).

**الحل العملي والآمن قانونيًا:**

| المصدر | الطريقة الموصى فيها | ملاحظة |
|---|---|---|
| Telegram Channels | **Telegram Bot API / MTProto (Telethon)** رسمي ومسموح | أفضل مصدر — منظم وسريع وقانوني 100% |
| RSS-enabled sites (Bayt, Indeed, Wuzzuf, Akhtaboot) | **RSS Feeds / Official APIs** | مصادر مستقرة وقانونية |
| LinkedIn | **LinkedIn Jobs API (محدود جدًا) أو عبر شريك رسمي** | تجنب scraping مباشر — خطر حظر قانوني |
| Facebook Groups | صعب آليًا، الحل: **مصدر يضيفه المستخدم + استخراج نصي بمساعدة LLM من نص يلصقه المستخدم أو رابط بوست** (Semi-automatic) | لتجنب الحظر، تبدأ بنظام "شارك رابط المنشور" بدل scraping كامل |
| مواقع الشركات مباشرة (Careers pages) | **Web scraping لصفحات وظائف عامة (مو منصات تواصل)** مسموح غالبًا وأخلاقي أكثر | هذا أسهل وأأمن جزء تبدأ فيه |

> **توصيتي:** ابدأ بـ Telegram + RSS + Careers pages (قانوني 100% وسهل تنفيذه)، وخلي فيسبوك/لينكدإن ميزة "شارك يدويًا + استخراج AI" بالمرحلة الثانية. هيك بتبني منتج شغال وآمن، وبعدين توسّع.

---

## 3. Tech Stack الكامل (مع بدائل موفرة للتكلفة)

| الطبقة | الخيار الأساسي (اللي طلبته) | البديل الأوفر/الأبسط |
|---|---|---|
| Backend | ASP.NET Core 8/9 (Microservices) | Modular Monolith بـ.NET بالبداية بدل Microservices كاملة (أرخص وأسهل نشر لحد ما يكبر المشروع) |
| Frontend | Angular 17+ | نفسه — خيار ممتاز، فيه دعم SSR (Angular Universal) للـ SEO |
| Database | PostgreSQL (عبر Supabase) | Supabase Free Tier كافي للبداية (500MB DB + Auth + Storage مجانًا) |
| Message Broker | RabbitMQ + MassTransit | CloudAMQP Free Tier (RabbitMQ مستضاف مجانًا لحد حد معين) |
| Auth | Supabase Auth | أو ASP.NET Identity + JWT إذا بدك تحكم أكثر |
| Search Engine | Meilisearch أو Elasticsearch | **Meilisearch** أخف وأرخص وأسهل استضافة من Elasticsearch، ومناسب جدًا لهالحجم |
| Vector DB (لـ RAG) | pgvector (extension على نفس Postgres/Supabase) | **الأفضل ماديًا** — ما بتحتاج Pinecone/Weaviate منفصل، pgvector مجاني وجزء من Supabase |
| LLM Provider | Anthropic Claude API / OpenAI | ابدأ بـ **نموذج صغير/رخيص** (Claude Haiku أو GPT-4o-mini) للمهام الروتينية (تصنيف، استخراج بيانات)، واستخدم نموذج أقوى بس للمحادثة/الـRAG المعقد |
| Notifications | SignalR (Real-time) + Telegram Bot API + Email (SMTP) | ابدأ بـ Telegram Bot (مجاني تمامًا وسريع) بدل Push notifications المكلفة |
| Hosting Backend | Azure App Service / Container Apps | **Railway / Render / Fly.io** أرخص بكثير للمراحل الأولى من Azure |
| Hosting Frontend | Azure Static Web Apps | **Vercel / Netlify** مجاني تمامًا لـAngular build |
| CV Generation | مكتبة توليد PDF (QuestPDF لـ.NET) | QuestPDF مجاني (Community license) وممتاز لتوليد PDF احترافي |
| Background Jobs | Hangfire / Quartz.NET | Hangfire أسهل بالتكامل مع RabbitMQ |
| CI/CD | GitHub Actions | مجاني للمشاريع الشخصية |
| Monitoring/Logging | Serilog + Seq/Grafana | Serilog + ملفات + Sentry (Free tier) للأخطاء |

---

## 4. هيكلية الـ Microservices

```
┌─────────────────────────────────────────────────────────┐
│                     API Gateway (YARP)                    │
└───────┬──────────┬──────────┬──────────┬─────────────────┘
        │           │          │          │
   ┌────▼───┐  ┌────▼────┐ ┌──▼─────┐ ┌──▼──────────┐
   │ Auth   │  │ Sources  │ │ Jobs   │ │ Search/RAG  │
   │Service │  │ Service  │ │Service │ │ Service     │
   └────────┘  └────┬─────┘ └───┬────┘ └─────┬───────┘
                     │           │            │
              ┌──────▼───────────▼────────────▼──────┐
              │        RabbitMQ + MassTransit          │
              │       (Event Bus بين كل الخدمات)        │
              └──────┬───────────┬────────────┬────────┘
                     │           │            │
              ┌──────▼──┐  ┌────▼─────┐ ┌────▼────────┐
              │Ingestion │  │Notification│ │ CV/ATS     │
              │ Workers  │  │ Service   │ │ Service     │
              │(Telegram,│  │(Telegram  │ │(QuestPDF)   │
              │ RSS, Web)│  │Bot/Email) │ │             │
              └──────────┘  └───────────┘ └─────────────┘
```

### شرح كل Service
- **Auth Service**: تسجيل دخول/خروج، JWT، إدارة بروفايل المستخدم وتفضيلاته الوظيفية.
- **Sources Service**: إدارة مصادر الوظائف (قنوات تيليجرام، RSS feeds، مواقع شركات) اللي يضيفها الأدمن أو المستخدم، مع حالة (نشط/معلق/مرفوض).
- **Jobs Service**: تخزين الوظائف بعد استخراجها ومعالجتها (CRUD + فلترة + تصنيف).
- **Ingestion Workers**: هي اللي "تسحب" الوظائف من المصادر بشكل دوري (Scheduled Jobs عبر Hangfire) وترسلها كـ Event على RabbitMQ.
- **Search/RAG Service**: يبني الـ Embeddings للوظائف (عبر LLM) ويخزنها بـ pgvector، ويعالج طلبات البحث الذكي.
- **Notification Service**: يستمع لأحداث "وظيفة جديدة تطابق تفضيلات مستخدم" ويرسل إشعار.
- **CV/ATS Service**: بناء وتوليد الـCV، وتحليل مدى توافقه مع وظيفة معينة (ATS Score).

> **ملاحظة مهمة للتكلفة:** ابدأ بـ **Modular Monolith** (كل الخدمات فوق بمشروع .NET واحد لكن بطبقات/Modules منفصلة منطقيًا)، واستخدم MassTransit مع **In-Memory Transport** بالتطوير. لما يكبر المشروع فعليًا وتحتاج Scale منفصل لكل جزء، افصلهم لـ Microservices حقيقية. هيك بتوفر وقت DevOps وتكلفة استضافة بالمرحلة الأولى وبضل التصميم قابل للفصل لاحقًا (Clean Architecture بتخليك جاهز لهالانتقال).

---

## 5. ERD — تصميم قاعدة البيانات الكامل

### الجداول الرئيسية

**Users**
- Id (PK, Guid)
- Email, PasswordHash (أو Supabase Auth Id)
- FullName, Phone
- PreferredJobTitles (jsonb/array)
- PreferredLocations (jsonb/array)
- PreferredSkills (jsonb/array)
- ExperienceLevel (enum: Entry/Mid/Senior)
- CreatedAt, UpdatedAt

**Sources** (المصادر)
- Id (PK)
- Name, Type (enum: TelegramChannel, RssFeed, CompanyCareersPage, ManualShare)
- Url / Identifier
- AddedByUserId (FK → Users, nullable لو أضافه أدمن)
- Status (enum: Pending, Active, Rejected, Paused)
- LastFetchedAt
- FetchIntervalMinutes
- CreatedAt

**RawPosts** (المنشورات الخام قبل المعالجة)
- Id (PK)
- SourceId (FK → Sources)
- RawContent (text)
- RawUrl
- FetchedAt
- ProcessingStatus (enum: New, Processing, Processed, Rejected)

**Jobs** (الوظائف بعد الاستخراج والمعالجة بـLLM)
- Id (PK)
- RawPostId (FK → RawPosts, nullable)
- SourceId (FK → Sources)
- Title, CompanyName
- Description (text)
- Location, IsRemote (bool)
- EmploymentType (enum: FullTime, PartTime, Freelance, Internship)
- ExperienceLevel
- SkillsRequired (jsonb array)
- SalaryRange (nullable)
- ExternalApplyUrl
- EmbeddingVector (vector — pgvector)
- PostedAt, ExpiresAt
- IsActive (bool)
- ViewsCount, ApplicantsClickCount

**JobCategories** / **Skills** (Lookup tables)
- Id, Name, Slug

**JobSkillsMap** (Many-to-Many بين Jobs و Skills)

**UserSavedJobs**
- UserId (FK), JobId (FK), SavedAt

**UserJobApplications** (تتبّع تقديمات المستخدم)
- Id, UserId, JobId, AppliedAt, Status (enum)

**Notifications**
- Id, UserId, JobId, Channel (enum: Telegram, Email, InApp), SentAt, Status

**CVs**
- Id, UserId (FK)
- TemplateId (FK → CVTemplates)
- ContentJson (بيانات الـCV هيكلية: خبرات، تعليم، مهارات...)
- GeneratedPdfUrl
- CreatedAt, UpdatedAt

**CVTemplates**
- Id, Name, PreviewImageUrl, IsAtsFriendly (bool)

**CVJobMatchAnalysis** (تحليل توافق CV مع وظيفة معينة)
- Id, CvId (FK), JobId (FK)
- AtsScore (0-100)
- MissingKeywords (jsonb array)
- Suggestions (text — من LLM)
- AnalyzedAt

### العلاقات (باختصار)
```
Users 1---N Sources (المستخدم ممكن يضيف مصادر)
Sources 1---N RawPosts
RawPosts 1---1 Jobs (بعد المعالجة)
Jobs N---N Skills (عبر JobSkillsMap)
Users N---N Jobs (عبر UserSavedJobs, UserJobApplications)
Users 1---N CVs
CVs 1---N CVJobMatchAnalysis
Jobs 1---N CVJobMatchAnalysis
Users 1---N Notifications
```

> ✅ **توصية تنفيذ:** استخدم `pgvector` extension على نفس جدول `Jobs` (عمود `embedding vector(1536)` مثلاً)، هيك ما بتحتاج قاعدة بيانات منفصلة للـ vector search — بتوفر تكلفة وتعقيد كبير.

---

## 6. نظام البحث الذكي (RAG) — "أسطوري" فعلاً

### خط سير العمل (Pipeline)
1. **عند إدخال وظيفة جديدة**: يتم إرسالها لـ LLM لاستخراج بيانات منظمة (Title, Skills, Location...) من النص الخام — Structured Output بصيغة JSON.
2. **توليد Embedding** للوصف الكامل للوظيفة (عبر embedding model) وتخزينه بعمود `vector` في نفس جدول `Jobs`.
3. **عند بحث المستخدم**: نص البحث يتحول لـ Embedding، ثم Similarity Search (Cosine Distance) بـ pgvector لإيجاد أقرب الوظائف دلاليًا — مو بس مطابقة كلمات.
4. **دمج نتائج البحث الدلالي + الفلاتر التقليدية** (Location, Salary, Type) عبر Hybrid Search.
5. **RAG للمحادثة**: لو بدك خانة "اسأل عن وظيفتك المثالية" (Chat)، الـLLM يسترجع أقرب N وظائف من pgvector كـ Context ويرد عليها بشكل طبيعي.

### لسرعة الاستجابة العالية
- **Caching**: استخدم Redis لتخزين نتائج البحث الشائعة والـ Embeddings المتكررة.
- **Async Processing**: توليد الـ Embeddings يصير بالخلفية (Background Worker) مو أثناء طلب المستخدم.
- **Batch Embeddings**: عالج عدة وظائف دفعة وحدة بدل واحدة واحدة لتقليل عدد نداءات الـAPI.
- **نموذج LLM خفيف** للمهام الروتينية (استخراج/تصنيف) ونموذج أقوى بس للمحادثة.

---

## 7. نظام الـ ATS CV Builder

### المزايا
1. **بناء تفاعلي**: نموذج خطوة بخطوة (خبرات، تعليم، مهارات، مشاريع).
2. **قوالب ATS-Friendly**: تصاميم بسيطة نصية (بدون جداول/صور معقدة) عشان أنظمة الـ ATS تقدر تقرأها.
3. **تحليل التوافق (Match Score)**: يقارن محتوى الـCV مع وصف وظيفة معينة عبر LLM، ويطلع:
   - نسبة توافق (%)
   - كلمات مفتاحية ناقصة
   - اقتراحات تحسين محددة
4. **توليد PDF** عبر QuestPDF (مجاني، سريع، دقيق بالتنسيق).
5. **تصدير نسخ متعددة** حسب كل وظيفة (Tailored CV لكل تقديم).

---

## 8. UI / UX — تصميم الشاشات الرئيسية

### الصفحات الأساسية
1. **Landing Page**: شرح المنصة + عدد الوظائف المتاحة (Social proof) + CTA للتسجيل.
2. **Feed / Dashboard**: قائمة وظائف بتحديث لحظي (SignalR)، مع Badge "جديد" للوظائف الحديثة، فلاتر جانبية (مهارة، موقع، نوع دوام، مصدر).
3. **صفحة تفاصيل الوظيفة**: الوصف الكامل + زر "تحقق من توافق CV" (يستدعي CVJobMatchAnalysis) + زر تقديم مباشر.
4. **صفحة البحث الذكي / Chat**: خانة نصية "احكيلي شو بتدور عليه" وترجعلك نتائج مرتبة دلاليًا، مع إمكانية حفظ البحث كـ "تنبيه دائم" (Saved Search Alert).
5. **إدارة المصادر (Sources)**: المستخدم يضيف رابط قناة تيليجرام/RSS/صفحة وظائف شركة، وتنتظر موافقة (Pending) أو تفعل تلقائيًا حسب الثقة.
6. **CV Builder**: واجهة Form متعددة الخطوات + معاينة PDF لحظية (Live Preview) + اختيار قالب.
7. **صفحة الإشعارات**: قائمة الإشعارات + إعدادات (تفعيل Telegram Bot، تكرار الإيميل).
8. **بروفايل المستخدم**: تفضيلات البحث (تُستخدم لتخصيص الإشعارات والـ Feed).

### مبادئ UX مهمة
- **Real-time indicators**: نقطة خضراء "Live" على الوظائف الجديدة عبر SignalR بدون Refresh.
- **Progressive disclosure**: ما تعرضش كل التفاصيل دفعة وحدة — كارد مختصر ثم تفاصيل عند الضغط.
- **Mobile-first**: أغلب المستخدمين رح يفتحوا من الموبايل (خصوصًا من تيليجرام) — صمم Angular بـ Responsive كامل من البداية.
- **Empty states واضحة**: لما ما في نتائج، وجّه المستخدم لإضافة مصدر أو تعديل الفلاتر.

---

## 9. نظام Error Handling الاحترافي

- **Global Exception Middleware** بـASP.NET Core يلتقط كل الأخطاء ويرجع شكل موحد (Problem Details — RFC 7807).
- **Custom Exception Types** لكل Domain (`JobNotFoundException`, `SourceAlreadyExistsException`...) بدل استخدام Exceptions عامة.
- **Result Pattern** (بدل رمي Exceptions بالـ business logic العادي) — يرجع `Result<T>` فيه Success/Failure + رسالة واضحة، ويحتفظ بالـ Exceptions للحالات الاستثنائية فعلاً.
- **Retry Policies** عبر Polly لأي نداء خارجي (LLM API، Telegram API، RSS fetch) مع Exponential Backoff.
- **Circuit Breaker** (Polly) عشان لو خدمة خارجية وقعت، ما يضلش النظام يحاول عليها ويبطئ كل شي.
- **Dead Letter Queue** بـ RabbitMQ/MassTransit لأي Event فشل معالجته أكثر من مرة، يروح لصف منفصل للمراجعة اليدوية.
- **Structured Logging** عبر Serilog مع Correlation ID لكل Request يتتبع عبر كل الـ Microservices.
- **Sentry/Application Insights** لتتبع الأخطاء بالإنتاج بشكل لحظي.

---

## 10. نظام Validation عالي المستوى

- **FluentValidation** لكل الـ DTOs بدل Data Annotations البسيطة — قواعد معقدة وقابلة لإعادة الاستخدام.
- **Validation على مستوى الطبقات الثلاث**:
  1. Frontend (Angular Reactive Forms + Validators) — تجربة فورية للمستخدم.
  2. API Layer (FluentValidation Pipeline Behavior مع MediatR).
  3. Domain Layer (Invariants داخل الـ Entities نفسها — مثلاً وظيفة ما إلها تاريخ انتهاء بالماضي).
- **Sanitization** لأي محتوى نصي خام قادم من مصادر خارجية (RawPosts) قبل عرضه — لتفادي XSS.
- **Rate Limiting** (ASP.NET Core built-in Rate Limiting) لحماية الـ API من إساءة الاستخدام، خصوصًا على endpoint إضافة المصادر.

---

## 11. مراحل التنفيذ المقترحة (Roadmap)

### المرحلة 0 — التحضير (أسبوع)
- إعداد Repo (Clean Architecture solution)، Supabase project، CI/CD أساسي.
- تصميم الـERD النهائي وتنفيذه كـ Migrations.

### المرحلة 1 — MVP أساسي (3-4 أسابيع)
- Auth (Supabase Auth + JWT).
- Sources Service (إضافة/عرض مصادر RSS + Telegram فقط).
- Ingestion Worker بسيط لـRSS + Telegram (عبر Telethon أو Bot API).
- Jobs Service (تخزين وعرض بسيط، بدون AI بعد).
- Frontend: صفحة Feed بسيطة + تسجيل دخول.

### المرحلة 2 — الذكاء (RAG + Search) (3 أسابيع)
- تكامل LLM لاستخراج بيانات منظمة من RawPosts.
- pgvector + Embeddings + Semantic Search.
- صفحة بحث ذكي/Chat.

### المرحلة 3 — التفاعل اللحظي والإشعارات (2 أسبوع)
- SignalR للـ Feed اللحظي.
- Notification Service (Telegram Bot أولاً، Email لاحقًا).
- Saved Searches / Alerts.

### المرحلة 4 — ATS CV Builder (2-3 أسابيع)
- CV Builder UI (Multi-step form).
- QuestPDF لتوليد الملفات.
- CVJobMatchAnalysis عبر LLM.

### المرحلة 5 — تحسينات الإنتاج (مستمر)
- Error Handling الكامل (Polly, Global Middleware).
- Validation الشامل.
- تحويل من Modular Monolith لـ Microservices حقيقية إذا احتجت (فصل RabbitMQ فعليًا بين خدمات منفصلة النشر).
- إضافة مصادر Facebook/LinkedIn بطريقة شبه-يدوية (Share & Extract).

---

## 12. Prompts جاهزة لاستخدامها في Antigravity

> استخدم هالـ Prompts بالترتيب، وحدّث الـ context (اسم المشروع، الفولدر...) حسب الحاجة. كل Prompt مصمم يكون Self-contained قد الإمكان.

### Prompt 1 — إعداد الـ Solution الأساسي
```
Create a .NET 8 solution named "JobRadar" using Clean Architecture with the following projects:
- JobRadar.Domain (entities, enums, no dependencies)
- JobRadar.Application (use cases, MediatR handlers, FluentValidation validators, interfaces)
- JobRadar.Infrastructure (EF Core with PostgreSQL, repository implementations, external service clients)
- JobRadar.Api (ASP.NET Core Web API, controllers, global exception middleware returning RFC 7807 Problem Details)
- JobRadar.Workers (background workers using Hangfire for scheduled ingestion jobs)

Use MediatR for CQRS, FluentValidation for request validation via a MediatR pipeline behavior, and Serilog for structured logging with correlation IDs. Configure EF Core to connect to a PostgreSQL database (Supabase-hosted) and enable the pgvector extension.
```

### Prompt 2 — الـ ERD والـEntities
```
In JobRadar.Domain, create entities for: User, Source (enum SourceType: TelegramChannel, RssFeed, CompanyCareersPage, ManualShare; enum SourceStatus: Pending, Active, Rejected, Paused), RawPost, Job (with a float[] or vector property for embeddings, EmploymentType enum, ExperienceLevel enum), Skill, JobSkillMap, UserSavedJob, UserJobApplication, Notification, Cv, CvTemplate, CvJobMatchAnalysis.
Define relationships as described: [paste the ERD relationships section from this document here].
Generate EF Core configurations (IEntityTypeConfiguration) for each entity in Infrastructure, including the pgvector column mapping for Job.Embedding using the Pgvector.EntityFrameworkCore package. Generate the initial migration.
```

### Prompt 3 — Sources Ingestion (Telegram + RSS)
```
Implement an ingestion pipeline in JobRadar.Workers:
1. A Hangfire recurring job that runs every N minutes per active Source.
2. For SourceType.RssFeed: fetch and parse the feed using CodeHollow.FeedReader, create a RawPost per new item.
3. For SourceType.TelegramChannel: use the WTelegramClient (or TDLib) library to fetch recent messages from a public channel, create a RawPost per message.
4. Publish a "RawPostCreated" event via MassTransit to RabbitMQ after each RawPost is saved.
Include Polly retry policies (3 retries, exponential backoff) around all external calls, and log failures with Serilog including the SourceId and correlation ID.
```

### Prompt 4 — LLM Extraction + Embeddings
```
Create a consumer in JobRadar.Infrastructure that listens for "RawPostCreated" events via MassTransit. For each event:
1. Call an LLM (Anthropic Claude API) with a structured-output prompt asking it to extract: Title, CompanyName, Location, IsRemote, EmploymentType, ExperienceLevel, SkillsRequired (array), SalaryRange if present — return strict JSON only, no markdown.
2. If extraction confidence is low or the content is not a real job posting, mark RawPost.ProcessingStatus = Rejected.
3. Otherwise, create a Job entity from the extracted data.
4. Generate an embedding vector for the job's full description using an embeddings model and store it in the Job.Embedding column (pgvector).
5. Publish a "JobCreated" event.
Wrap all LLM calls in a Polly retry + circuit breaker policy, and process embeddings generation as a batched background task where possible for performance.
```

### Prompt 5 — Semantic Search Endpoint
```
Create a JobRadar.Application use case "SearchJobsQuery" that:
1. Accepts a free-text query plus optional filters (location, employment type, experience level).
2. Generates an embedding for the query text.
3. Runs a hybrid search against the Jobs table in PostgreSQL: cosine similarity via pgvector (<=> operator) combined with the structured filters, ordered by similarity score.
4. Returns a paginated result with a relevance score per job.
Expose this via a JobsController endpoint `POST /api/jobs/search`. Add Redis caching for repeated identical queries with a short TTL (e.g. 5 minutes).
```

### Prompt 6 — Angular Real-time Feed
```
In the Angular 17 frontend, create a "JobFeed" feature module with:
1. A JobFeedComponent showing a paginated, filterable list of job cards (title, company, location, posted time, skills as chips).
2. A SignalR service that connects to a "/hubs/jobs" hub and prepends newly broadcasted jobs to the top of the list with a "New" badge, without a full page refresh.
3. Reactive Forms for the filter sidebar (skills multi-select, location, employment type, experience level) that debounce input and call the search API.
Use Angular signals for state management of the job list. Style with a clean, professional, mobile-first responsive layout.
```

### Prompt 7 — ATS CV Builder + PDF
```
Create a Cv Builder feature:
Backend (JobRadar.Application/Infrastructure):
1. A CvsController with endpoints to create/update a Cv (stored as structured JSON: PersonalInfo, Experiences[], Education[], Skills[], Projects[]).
2. A PDF generation service using QuestPDF that renders an ATS-friendly single-column template from the Cv JSON (no tables/images, standard fonts, clear section headers).
3. A CvJobMatchAnalysis use case that sends the Cv content and a target Job description to the LLM, asking it to return a JSON with: atsScore (0-100), missingKeywords (array), suggestions (array of short actionable strings).

Frontend (Angular):
1. A multi-step reactive form (Personal Info → Experience → Education → Skills → Projects) with a live preview panel rendering the current Cv state.
2. A "Check ATS Match" button on the job details page that calls the analysis endpoint and displays the score with a progress ring, missing keywords as chips, and suggestions as a list.
```

### Prompt 8 — Notifications عبر Telegram Bot
```
Implement a Notification Service that:
1. Consumes "JobCreated" events via MassTransit.
2. Matches the new job against each active user's saved search preferences (skills, location, employment type) using a simple scoring function combined with the job's embedding similarity to a "preference embedding" generated once from the user's profile.
3. For matches above a threshold, sends a formatted message via the Telegram Bot API to the user's linked Telegram chat ID, and creates a Notification record.
4. Expose an endpoint for users to link their Telegram account (generate a one-time deep link `t.me/YourBot?start=<token>` that the bot resolves to the UserId).
Wrap the Telegram API call in a retry policy, and if it fails after retries, fall back to creating an in-app Notification only.
```

### Prompt 9 — Error Handling + Validation احترافي (Global Pass)
```
Review the entire JobRadar solution and:
1. Ensure every controller action goes through a global exception handling middleware that maps custom domain exceptions (e.g., SourceAlreadyExistsException, JobNotFoundException) to appropriate HTTP status codes and RFC 7807 ProblemDetails responses, with a generic 500 handler that logs the full exception via Serilog but returns a safe message to the client.
2. Add a MediatR pipeline behavior that runs FluentValidation validators before every command/query handler and returns a structured validation error response (Result pattern with field-level errors) on failure.
3. Add rate limiting (ASP.NET Core built-in) on the /api/sources POST endpoint and the /api/jobs/search endpoint.
4. Configure a MassTransit dead-letter/error queue so any consumer that fails repeatedly moves messages to an error queue instead of retrying indefinitely, and log those occurrences.
```

---

## 13. ملاحظات ختامية

- **ابدأ صغير**: خلي الـ MVP فقط RSS + Telegram + بحث بسيط، وأثبت إنه المفهوم شغال (Sources كافية، الناس فعلاً بتستخدمه) قبل ما تستثمر وقت بالـ RAG والـATS الكاملين.
- **راقب التكلفة أسبوعيًا**: خصوصًا نداءات الـLLM (استخراج + embeddings) لأنها أكثر جزء ممكن يكلف مع نمو عدد المنشورات — استخدم Batching و caching بقوة.
- **قانونيًا**: خليك دايمًا ضمن "Sources يضيفها المستخدم" أو "APIs/RSS رسمية" لتفادي أي مشاكل قانونية مع منصات التواصل — هاي أهم نقطة تحافظ فيها على استمرارية المشروع.
- الملف هذا نقطة انطلاق — عدّل الـERD والمراحل حسب ما يتوضح لك أثناء التنفيذ الفعلي.

---

## 14. بخصوص LinkedIn تحديدًا — لازم تعرف هاد قبل لا تبني عليه

بصراحة معك، وهاي نقطة **حرجة** لازم تاخدها بجدية لأنها ممكن تسقط المشروع كامل لو تجاهلتها:

- LinkedIn عندها من أقوى أنظمة الحماية ضد الـ Scraping بالعالم (Anti-bot detection متطور جدًا + حظر IP/حساب فوري).
- فيه سابقة قانونية معروفة (**hiQ Labs vs LinkedIn**) بينت إن LinkedIn قادرة تلاحق قانونيًا أي جهة تعمل scraping آلي بشكل تجاري.
- أي مكتبة "unofficial" لـLinkedIn scraping اللي بتلاقيها على GitHub بتنكسر باستمرار وبتحتاج صيانة دائمة، وبتخاطر بحظر حسابك الشخصي لو ربطتها فيه.

**فلهيك ما رح أعطيك طريقة automated scraping مباشر لـLinkedIn** — مش لأنه صعب تقنيًا، إنما لأنه فعليًا بكسر شروط الاستخدام وبعرضك لمخاطر حقيقية (قانونية وحظر). بس هاد مو معناه إنك ما تقدر تغطي LinkedIn، عندك 3 طرق شرعية وقوية:

### الطريقة 1 (الأقوى والأسلم) — Browser Extension "Capture Assist"
بدل ما "تروح تجيب" الوظائف من LinkedIn، اعمل **إضافة متصفح (Chrome Extension)** بسيطة:
- المستخدم يتصفح LinkedIn بشكل طبيعي بحسابه الشخصي (مش bot).
- لما يوقف على منشور وظيفة، يضغط زر "Save to JobRadar" بالإضافة.
- الإضافة تقرأ محتوى الصفحة اللي هو أصلاً فاتحها (DOM اللي المتصفح حمّله لحسابه)، وترسله لـ backend عندك.
- هاد أسلوب مستخدم فعليًا بأدوات معروفة (متل Teal, Simplify, Huntr) وهو **أخف بكثير من الـ Scraping الآلي** لأنه فعل بشري حقيقي مو bot، بس خليك واعي إنه برضو تقنيًا داخل منطقة رمادية من ناحية شروط LinkedIn (بس المخاطرة القانونية شبه معدومة لأنه مش تجميع آلي جماعي).

### الطريقة 2 — LinkedIn's own RSS-like / Job Alert Emails
LinkedIn بيبعت "Job Alert" إيميلات دورية حسب بحث محفوظ. تقدر:
- تعمل حساب Gmail مخصص، تعمل عليه Job Alerts لبحثات متنوعة.
- تربط Gmail API (مجاني بالكامل ضمن الحدود العادية) تقرأ هاي الإيميلات وتستخرج منها الوظائف بـLLM.
- شرعي 100% لأنه أنت مشترك فعليًا بالتنبيه.

### الطريقة 3 — LinkedIn Talent/Jobs API الرسمي
فيه LinkedIn Talent Solutions API لكنه **محدود جدًا ومخصص لشركات توظيف موثّقة**، مو متاح للأفراد بسهولة — اذكرها كخيار مستقبلي إذا كبر المشروع وصار له شراكات رسمية.

> **توصيتي الصريحة:** ابدأ بالـ Browser Extension (طريقة 1) — هاي فعلاً بتخليك "أسطوري" لأنها ميزة مميزة (مو موجودة عند أغلب المنافسين المحليين)، وبنفس الوقت آمنة وما بتحتاج بنية تحتية معقدة أو تعرضك لحظر.

---

## 15. نظام "أسطوري" فعلاً — مزايا مستوى عالمي (بميزانية صفر)

هاي المزايا يلي بتفرّق بين مشروع تخرج عادي ومشروع يقدر ينافس منتجات حقيقية:

1. **Trending Skills Radar** — تحليل لحظي لأكثر المهارات المطلوبة بالسوق (مبني من بيانات الوظائف المجمّعة نفسها، بدون أي API خارجي إضافي) — Dashboard فيه Charts (D3.js/Chart.js).
2. **Salary Insights (تقديري)** — لما توفر بيانات كافية، استخدم LLM يستنتج نطاقات رواتب تقديرية حسب المسمى والموقع من الوصف النصي نفسه (مجاني، من بياناتك).
3. **AI Auto-Draft Cover Letter** — بناءً على الـCV + وصف الوظيفة، LLM يسوي مسودة رسالة تقديم جاهزة بثواني (استخدم نفس LLM المستخدم بالـ ATS Analysis، ما فيه تكلفة إضافية).
4. **"One-Click Tailor"** — نسخة CV معدّلة أوتوماتيكيًا لكل وظيفة (يبرز الكلمات المفتاحية المطابقة) — دقيقة استخدام LLM وحدة لكل تقديم.
5. **Community Verified Sources** — نظام تصويت (Upvote/Downvote) على المصادر اللي يضيفها المستخدمين، لضمان الجودة بدون ما تحتاج فريق مراجعة يدوي (Crowdsourced Trust Score).
6. **Smart Digest عبر Telegram** — بدل إشعار لكل وظيفة، ملخص ذكي يومي/أسبوعي مرتب حسب توافقك (تجربة مستخدم أرقى من إشعارات مزعجة).
7. **Public API مجاني محدود** — افتح جزء من بياناتك (Jobs) كـ API عام مجاني بحدود معينة (Rate limited) — بيجيب مطورين ومشاريع تانية تعتمد عليك وتصير مرجع بالمجال (زي إستراتيجية Product Hunt/GitHub API).

---

## 16. Zero-Cost Stack — بديل مجاني 100% لكل مكوّن

الهدف: تبني وتشغّل المشروع كامل بميزانية **$0** لحد ما ياخد Traction حقيقي.

| المكوّن | البديل المجاني بالكامل | الحد المجاني |
|---|---|---|
| Backend Hosting | **Oracle Cloud "Always Free" Tier** | 4 ARM cores + 24GB RAM **للأبد مجانًا** (الأقوى بالسوق فعليًا) — تقدر تشغّل عليها كل شي: API + RabbitMQ + Meilisearch |
| بديل أسهل نشرًا | Railway/Render Free tier أو Fly.io | كافي للـMVP، أبسط بالإعداد من Oracle |
| Database | Supabase Free | 500MB + Auth + Storage مجاني للأبد |
| Vector Search | pgvector (جزء من نفس Supabase Postgres) | مجاني — بدون خدمة منفصلة |
| Full-text Search | Meilisearch self-hosted على Oracle Free VM | مجاني بالكامل (بدل Meilisearch Cloud المدفوع) |
| Message Broker | RabbitMQ self-hosted على نفس Oracle VM (Docker) | مجاني بالكامل بدل CloudAMQP المدفوع |
| Cache | Redis self-hosted (Docker) على نفس VM، أو Upstash Free tier | مجاني |
| LLM (استخراج + تصنيف) | **Groq API** (نماذج Llama/Mixtral مجانية بحدود سخية جدًا وسرعة استجابة خيالية) | مجاني بحدود يومية كبيرة، وأسرع LLM API موجود حاليًا |
| LLM (مهام أعمق) | Google Gemini API (Free tier سخي) أو Claude/OpenAI بحدود مجانية للتجربة | استخدمه بحذر وبكميات أقل |
| Embeddings | نموذج مفتوح المصدر محلي (`sentence-transformers` عبر ONNX أو Python microservice صغير) بدل استدعاء API مدفوع | مجاني تمامًا، يشتغل على نفس السيرفر |
| بديل embeddings أسهل | Cohere Free tier أو Google text-embedding (Free tier) | مجاني بحدود |
| Frontend Hosting | Vercel / Netlify / Cloudflare Pages | مجاني بالكامل للأبد لمشروع Angular |
| Notifications | Telegram Bot API | مجاني 100% بدون أي حدود عملية |
| Background Jobs | Hangfire (مفتوح المصدر، يشتغل على نفس السيرفر) | مجاني |
| CV → PDF | QuestPDF (Community License مجاني لمشاريع صغيرة/غير ربحية) | مجاني ضمن شروط الترخيص |
| Monitoring | Grafana + Prometheus self-hosted، أو Sentry Free tier | مجاني |
| CI/CD | GitHub Actions | مجاني للمستودعات العامة/الشخصية |
| Domain | ابدأ بـ subdomain مجاني (Vercel/Render يعطوك واحد) لحد توفر لدومين مدفوع لاحقًا | $0 بالبداية |

> **الخلاصة:** سيرفر Oracle Cloud Free واحد قوي (4 ARM + 24GB RAM) يقدر يستضيف عليه: API + RabbitMQ + Meilisearch + Redis + Embeddings microservice كلهم مع بعض بـ Docker Compose — وده عمليًا **بنية تحتية كاملة الاحترافية بصفر تكلفة شهرية**، والشي الوحيد المدفوع المحتمل مستقبلًا هو لو تجاوزت الحدود المجانية لـLLM API مع نمو الاستخدام.
