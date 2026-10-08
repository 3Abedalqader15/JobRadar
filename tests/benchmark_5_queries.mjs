const baseUrl = 'http://localhost:5000/api/jobs/search';

const queries = [
  {
    name: 'Query 1: Tech Synonym Expansion (".NET")',
    payload: { query: '.NET', page: 1, pageSize: 5 },
    notes: 'Tests configurable tech synonym map (.NET -> C#, dotnet, asp.net, csharp)'
  },
  {
    name: 'Query 2: Typo Tolerance ("Pythn")',
    payload: { query: 'Pythn', page: 1, pageSize: 5 },
    notes: 'Tests pg_trgm fuzzy matching against misspelled job title ("Python")'
  },
  {
    name: 'Query 3: Typo Tolerance on Company ("Microsft")',
    payload: { query: 'Microsft', page: 1, pageSize: 5 },
    notes: 'Tests pg_trgm fuzzy matching against misspelled company name'
  },
  {
    name: 'Query 4: Remote Intent Understanding ("work from home react")',
    payload: { query: 'work from home react', page: 1, pageSize: 5 },
    notes: 'Tests conversational remote intent detection + synonym expansion (react -> reactjs, frontend)'
  },
  {
    name: 'Query 5: Filter Relaxation & "No Results" Recovery ("Developer" with SalaryMin $250,000)',
    payload: { query: 'Developer', salaryMin: 250000, page: 1, pageSize: 5 },
    notes: 'Tests automatic filter broadening recovery when restrictive salary yields 0 results'
  },
  {
    name: 'Query 6: Ambiguous Natural Language Query ("entry level job for junior who wants to build mobile apps with Flutter")',
    payload: { query: 'entry level job for junior who wants to build mobile apps with Flutter', page: 1, pageSize: 5 },
    notes: 'Tests query understanding for long conversational query with keyword extraction & fuzzy matching'
  }
];

async function runBenchmark() {
  console.log('='.repeat(80));
  console.log('JOBRADAR SEARCH RELEVANCE ELEVATION BENCHMARK REPORT');
  console.log('='.repeat(80));

  for (const q of queries) {
    console.log(`\n▶ ${q.name}`);
    console.log(`  Notes: ${q.notes}`);
    console.log(`  Request: ${JSON.stringify(q.payload)}`);

    try {
      const res = await fetch(baseUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(q.payload)
      });

      if (!res.ok) {
        console.log(`  HTTP Error: ${res.status} ${res.statusText}`);
        continue;
      }

      const data = await res.json();
      console.log(`  Total Count: ${data.totalCount}`);
      if (data.broadeningNotice) {
        console.log(`  Broadening Notice: "${data.broadeningNotice}"`);
      }
      if (data.suggestedQuery) {
        console.log(`  Suggested Query: "${data.suggestedQuery}"`);
      }

      console.log(`  Top Items (${data.items?.length || 0}):`);
      for (const item of (data.items || []).slice(0, 3)) {
        console.log(`    - Title: "${item.title}"`);
        console.log(`      Company: "${item.companyName}" | Remote: ${item.isRemote} | Score: ${(item.relevanceScore * 100).toFixed(1)}%`);
        console.log(`      Matched Terms: [${(item.matchedTerms || []).join(', ')}]`);
      }
    } catch (err) {
      console.error(`  Failed to execute query: ${err.message}`);
    }
  }
  console.log('\n' + '='.repeat(80));
}

runBenchmark();
