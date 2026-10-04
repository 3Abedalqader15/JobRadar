using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Persistence;

/// <summary>
/// Seeds official Jordanian recruitment sources (Banks, Telecom, Tech, Logistics, Universities, Health, and Job Boards)
/// into the database so JobRadar's background workers monitor and fetch vacancies automatically.
/// </summary>
public static class JordanianSourcesSeeder
{
    public static async Task SeedJordanianSourcesAsync(AppDbContext db, ILogger logger)
    {
        logger.LogInformation("Checking and seeding official Jordanian job sources...");

        var sourcesToSeed = new (string Name, SourceType Type, string Url, int FetchIntervalMinutes)[]
        {
            // ── Banks (بنوك) ──────────────────────────────────────────────────
            ("Arab Bank (البنك العربي)", SourceType.CompanyCareersPage, "https://arabbank.taleo.net/careersection/ex/jobsearch.ftl", 60),
            ("Bank of Jordan (بنك الأردن)", SourceType.CompanyCareersPage, "https://apply.workable.com/bank-of-jordan/", 30),
            ("Jordan Ahli Bank (البنك الأهلي الأردني)", SourceType.CompanyCareersPage, "https://careers.ahli.com/", 60),
            ("Jordan Kuwait Bank (البنك الأردني الكويتي)", SourceType.CompanyCareersPage, "https://www.jkb.com/en/careers", 60),
            ("Cairo Amman Bank (بنك القاهرة عمان)", SourceType.CompanyCareersPage, "https://www.cab.jo/careers", 60),
            ("Capital Bank of Jordan (كابيتال بنك)", SourceType.CompanyCareersPage, "https://www.capitalbank.jo/en/personal/careers", 60),
            ("Arab Jordan Investment Bank (بنك الاستثمار العربي الأردني)", SourceType.CompanyCareersPage, "https://www.ajib.com/en/careers", 60),

            // ── Telecom (اتصالات) ──────────────────────────────────────────────
            ("Zain Jordan (زين الأردن)", SourceType.CompanyCareersPage, "https://careers.zain.com", 30),
            ("Orange Jordan (أورنج الأردن)", SourceType.CompanyCareersPage, "https://jobs.orange.jo", 30),
            ("Umniah (أمنية)", SourceType.CompanyCareersPage, "https://www.umniah.com/en/careers", 60),

            // ── Logistics & Technology (لوجستيات وتقنية) ──────────────────────
            ("Aramex (أرامكس)", SourceType.CompanyCareersPage, "https://www.aramex.com/careers", 60),
            ("Globitel (جلوبيتل)", SourceType.CompanyCareersPage, "https://globitel.com/careers/", 60),
            ("Estarta Solutions (إيستارتا)", SourceType.CompanyCareersPage, "https://www.estarta.com/careers/", 60),

            // ── Insurance (تأمين) ─────────────────────────────────────────────
            ("GIG Jordan Insurance (الشرق العربي للتأمين)", SourceType.CompanyCareersPage, "https://www.gig.com.jo/en/careers", 120),
            ("Islamic Insurance Jordan (التأمين الإسلامية)", SourceType.CompanyCareersPage, "https://www.islamic-insurance.com/careers", 120),

            // ── Healthcare (صحة ورعاية طبية) ─────────────────────────────────
            ("King Hussein Cancer Center (مركز الحسين للسرطان)", SourceType.CompanyCareersPage, "https://www.khcc.jo/en/careers", 60),

            // ── Education & Universities (تعليم وجامعات) ─────────────────────
            ("University of Jordan (الجامعة الأردنية)", SourceType.CompanyCareersPage, "https://www.ju.edu.jo/", 120),
            ("German Jordanian University (الجامعة الألمانية الأردنية)", SourceType.CompanyCareersPage, "https://www.gju.edu.jo/vacancies", 120),
            ("Princess Sumaya University (جامعة الأميرة سمية)", SourceType.CompanyCareersPage, "https://www.psut.edu.jo/en/page/employment", 120),
            ("Petra University (جامعة البترا)", SourceType.CompanyCareersPage, "https://www.uop.edu.jo/Ar/Careers/Pages/default.aspx", 120),

            // ── Public Sector (حكومي) ─────────────────────────────────────────
            ("هيئة الخدمة والإدارة العامة (ديوان الخدمة المدنية سابقاً)", SourceType.CompanyCareersPage, "https://applyjobs.spac.gov.jo", 60),

            // ── Job Boards & Aggregators (منصات ومجمعات التوظيف) ─────────────
            ("Akhtaboot Jordan (أخطبوط الأردن)", SourceType.CompanyCareersPage, "https://www.akhtaboot.com/ar/jordan/jobs", 30),
            ("Bayt.com Jordan (بيت.كوم الأردن)", SourceType.CompanyCareersPage, "https://www.bayt.com/ar/jordan/jobs/", 30),
            ("Tanqeeb Jordan (تنقيب الأردن)", SourceType.CompanyCareersPage, "https://jordan.tanqeeb.com/jobs/", 30),
            ("Waseet Jordan (الوسيط)", SourceType.CompanyCareersPage, "https://jo.waseet.net/ar/jobs", 60),
            ("LinkedIn Jordan Jobs (وظائف لينكدإن الأردن)", SourceType.LinkedIn, "https://www.linkedin.com/jobs/search?keywords=&location=Jordan", 30)
        };

        var existingUrls = await db.Sources
            .Select(s => s.Url.ToLowerInvariant())
            .ToListAsync();

        var existingUrlSet = new HashSet<string>(existingUrls);
        var addedCount = 0;

        foreach (var (name, type, url, interval) in sourcesToSeed)
        {
            if (!existingUrlSet.Contains(url.ToLowerInvariant()))
            {
                var source = Source.Create(
                    name: name,
                    type: type,
                    url: url,
                    addedByUserId: null,
                    fetchIntervalMinutes: interval
                );

                source.Approve(); // Mark as Active so workers immediately start fetching it
                db.Sources.Add(source);
                existingUrlSet.Add(url.ToLowerInvariant());
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Successfully seeded {Count} new Jordanian job sources into database.", addedCount);
        }
        else
        {
            logger.LogInformation("All Jordanian job sources already exist in database.");
        }
    }
}
