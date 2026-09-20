<div align="center">

<img src="https://capsule-render.vercel.app/api?type=waving&color=gradient&customColorList=6,11,20&height=230&section=header&text=JobRadar&fontSize=75&fontColor=ffffff&animation=fadeIn&fontAlignY=38&desc=The%20job%20doesn't%20come%20to%20you.%20So%20we%20built%20a%20radar%20that%20goes%20and%20gets%20it.&descAlignY=58&descSize=17&descAlign=50" width="100%"/>

<img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=22&duration=2500&pause=900&color=39D353&center=true&vCenter=true&multiline=true&repeat=true&width=760&height=100&lines=Real-time+job+aggregation+from+social+platforms+%F0%9F%93%A1;RAG-powered+semantic+search+with+pgvector+%F0%9F%A7%A0;ATS-grade+CV+scoring+in+real+time+%F0%9F%93%84;.NET+8+%2B+Angular+17+%2B+RabbitMQ+%2B+Gemini+%E2%9A%A1" alt="Typing SVG" />

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-17-DD0031?style=for-the-badge&logo=angular)](https://angular.io/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-pgvector-4169E1?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600?style=for-the-badge&logo=rabbitmq)](https://www.rabbitmq.com/)
[![Gemini](https://img.shields.io/badge/Gemini-AI%20Powered-8E75B2?style=for-the-badge&logo=googlegemini)](https://ai.google.dev/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](#license)

[Features](#-features) · [Architecture](#-architecture) · [Tech Stack](#-tech-stack) · [Getting Started](#-getting-started) · [Roadmap](#-roadmap)

<img src="https://skillicons.dev/icons?i=dotnet,cs,angular,postgres,redis,rabbitmq,docker,githubactions,git&theme=dark" alt="tech icons"/>

</div>

---

## 📡 What is JobRadar?

Job hunters don't lose because opportunities don't exist. They lose because opportunities are **scattered** — buried inside Telegram channels, Facebook groups, LinkedIn feeds, and a hundred career pages nobody checks daily.

**JobRadar hunts them down for you — the moment they're posted.**

It's not another job board. It's a **living, breathing aggregation engine** that:
- 🎯 Pulls postings from Telegram channels, RSS feeds, company career pages, and user-submitted sources — continuously, automatically.
- 🧠 Understands job descriptions semantically via **RAG + vector search**, not brittle keyword matching.
- ⚡ Notifies you the second a matching role drops, via Telegram or in-app.
- 📄 Builds you an **ATS-optimized CV**, scores it against any job in real time, and tells you exactly what's missing.
- 🌐 Grows itself — every user who adds a source makes the radar stronger for everyone.

---

## ✨ Features

### 🔍 Legendary Search, Not Keyword Roulette
Hybrid search combining **pgvector cosine similarity** with structured filters (location, seniority, employment type). Search *"remote backend role, not too senior, good work-life balance"* and get results that actually understand what you meant.

### 📥 Multi-Source, Real-Time Ingestion
| Source | Method |
|---|---|
| Telegram Channels | Official Bot API / MTProto — legal, stable |
| RSS Feeds | Standard feed parsing |
| Company Career Pages | Ethical, targeted scraping |
| LinkedIn / Facebook | Browser-extension "Capture Assist" — powered by the user's own session, no ToS-violating bot scraping |
| User-Submitted Sources | Community-driven, crowdsourced trust scoring |

### 🧠 RAG-Powered Intelligence
Every job posting is embedded, indexed with **HNSW vector indexing**, and made searchable in milliseconds. An LLM extraction pipeline (Gemini) structures raw, messy text into clean, queryable data — automatically, at ingestion time.

### 📄 ATS CV Builder — Built to Get You Past the Bots
- Multi-step CV builder with live PDF preview (QuestPDF).
- One-click **ATS Match Score** against any job posting.
- Missing-keyword detection + AI-generated improvement suggestions.
- Tailored CV variants, generated per application.

### 🔔 Notifications That Respect Your Attention
Telegram-first alerts, smart digest mode (daily/weekly instead of spam), and fully configurable match thresholds.

### 🏗️ Built Like It's Going to Production
Outbox pattern for guaranteed event delivery, idempotent consumers, Polly-backed resilience (retry + circuit breaker), FluentValidation across every boundary, rate limiting on expensive endpoints, and structured logging with correlation IDs end-to-end.

---

## 🏛️ Architecture

```mermaid
flowchart TB
    subgraph Client["🖥️ Client Layer"]
        ANG[Angular 17 SPA]
        EXT[Browser Extension]
    end

    subgraph Gateway["🚪 API Gateway"]
        API[ASP.NET Core API]
    end

    subgraph Core["⚙️ Application Core — Clean Architecture"]
        DOM[Domain]
        APP[Application — CQRS / MediatR]
        INF[Infrastructure]
    end

    subgraph Bus["📨 Event Bus"]
        MT[MassTransit + RabbitMQ<br/>Outbox Pattern]
    end

    subgraph Workers["👷 Background Workers"]
        ING[Ingestion Workers<br/>Telegram · RSS · Careers]
        LLM[LLM Extraction Consumer<br/>Gemini Structured Output]
        EMB[Embedding Batch Processor]
        NOT[Notification Service]
    end

    subgraph Data["🗄️ Data Layer"]
        PG[(PostgreSQL + pgvector<br/>Supabase)]
        REDIS[(Redis Cache)]
    end

    ANG --> API
    EXT --> API
    API --> APP --> DOM
    APP --> INF --> PG
    APP <--> REDIS
    INF --> MT
    MT --> ING & LLM & EMB & NOT
    ING --> MT
    LLM --> PG
    EMB --> PG
    NOT --> PG
```

### Design Principles
- **Clean Architecture** — Domain has zero dependencies; business logic lives in Application, not scattered across consumers or controllers.
- **CQRS via MediatR** — every use case is an explicit command or query, independently testable.
- **Modular Monolith → Microservices** — starts as one deployable unit for speed and cost, cleanly separable into real microservices once scale demands it.
- **Outbox Pattern** — no dual-write inconsistency between DB state and published events, ever.
- **Idempotency by default** — every consumer is safe to redeliver.

---

## 🧰 Tech Stack

| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core 8, Clean Architecture, MediatR (CQRS) |
| **Frontend** | Angular 17, Reactive Forms, Signals, SignalR (real-time feed) |
| **Database** | PostgreSQL (Supabase) + `pgvector` (HNSW-indexed) |
| **Messaging** | RabbitMQ + MassTransit (EF Core Outbox, Batch Consumers) |
| **AI / LLM** | Google Gemini (`gemini-1.5-flash` extraction, `text-embedding-004` embeddings) |
| **Search** | Hybrid semantic + structured filtering via `EF.Functions.CosineDistance` |
| **Caching** | Redis (two-layer: embedding cache + results cache) |
| **Resilience** | Polly (retry + circuit breaker on every external call) |
| **Validation** | FluentValidation (pipeline behavior across all commands/queries) |
| **Background Jobs** | Hangfire |
| **PDF Generation** | QuestPDF |
| **Auth** | Supabase Auth / JWT |
| **Hosting (Free-tier friendly)** | Oracle Cloud Always Free, Vercel/Netlify, Supabase |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) & Angular CLI
- Docker (for RabbitMQ + Redis locally)
- A [Supabase](https://supabase.com/) project (free tier)
- A [Google AI Studio](https://aistudio.google.com/) API key (Gemini)

### 1. Clone the repository
```bash
git clone https://github.com/3Abedalqader15/JobRadar.git
cd JobRadar
```

### 2. Spin up local infrastructure
```bash
docker compose up -d   # RabbitMQ + Redis
```

### 3. Configure secrets
```bash
cd src/JobRadar.Api
dotnet user-secrets set "ConnectionStrings:Default" "<your-supabase-postgres-connection-string>"
dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-api-key>"
```

### 4. Apply database migrations
```bash
dotnet ef database update --project src/JobRadar.Infrastructure --startup-project src/JobRadar.Api
```

### 5. Run the backend
```bash
dotnet run --project src/JobRadar.Api
dotnet run --project src/JobRadar.Workers
```

### 6. Run the frontend
```bash
cd client
npm install
ng serve
```

App runs at `http://localhost:4200` · API at `http://localhost:5000`

---

## 📂 Project Structure

```
JobRadar/
├── src/
│   ├── JobRadar.Domain/          # Entities, enums — zero dependencies
│   ├── JobRadar.Application/     # CQRS use cases, validators, abstractions
│   ├── JobRadar.Infrastructure/  # EF Core, external service clients, consumers
│   ├── JobRadar.Api/             # Controllers, middleware, composition root
│   └── JobRadar.Workers/         # Hangfire jobs, MassTransit consumers
├── client/                       # Angular 17 application
├── scratch/                      # Smoke tests / scratch space
└── JobRadar-Project-Plan.md      # Full project planning document
```

---

## 🗺️ Roadmap

- [x] Core domain model & ERD
- [x] Clean Architecture scaffolding
- [x] RSS + Telegram ingestion
- [x] LLM extraction pipeline (Gemini)
- [x] pgvector semantic search + Redis caching
- [ ] SignalR real-time feed
- [ ] ATS CV Builder + PDF generation
- [ ] Browser extension (LinkedIn/Facebook capture)
- [ ] Notification service (Telegram digest)
- [ ] Trending Skills Radar dashboard
- [ ] Public read-only API

---

## 🤝 Contributing

This is currently a solo-built portfolio project — but issues, ideas, and PRs are always welcome. Open an issue before submitting a large PR so we can align on direction first.

---

## 📄 License

Distributed under the MIT License. See `LICENSE` for details.

---

<div align="center">

**Built by [Abedalqader](https://github.com/3Abedalqader15)** — backend .NET developer, Irbid, Jordan 🇯🇴

*If JobRadar saved you from refreshing LinkedIn for the hundredth time, consider giving it a ⭐*

<!--
  🐍 Contribution Snake — animated SVG generated by GitHub Actions.
  To activate: add .github/workflows/snake.yml (included in this repo) and let it run once
  on push to main. It will auto-generate the two assets below on an "output" branch.
-->
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/3Abedalqader15/JobRadar/output/github-contribution-grid-snake-dark.svg" />
  <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/3Abedalqader15/JobRadar/output/github-contribution-grid-snake.svg" />
  <img alt="contribution snake animation" src="https://raw.githubusercontent.com/3Abedalqader15/JobRadar/output/github-contribution-grid-snake.svg" width="100%"/>
</picture>

<img src="https://capsule-render.vercel.app/api?type=waving&color=gradient&customColorList=6,11,20&height=120&section=footer" width="100%"/>

</div>
