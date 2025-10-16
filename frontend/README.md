## LaySumm Frontend

This Vite + React client wraps the LaySumm API so product teams can upload a source document, add an optional user prompt, and watch the summarisation workflow complete in real time. When the job finishes it renders the generated PDF and exposes download links for the other formats.

### Prerequisites

- Node.js 18+
- Access to a running instance of the LaySumm API (defaults to `http://localhost:5000`)

### Quick start

```bash
cd frontend
cp .env.example .env          # edit VITE_API_BASE_URL if the API is hosted elsewhere
npm install
npm run dev                   # Launches http://localhost:5173
```

The dev server proxies `/api/*` requests to `VITE_API_BASE_URL`, so your browser never needs direct blob credentials. For a production build run:

```bash
npm run build
```

Then host the generated `dist/` directory using your preferred static hosting platform or serve it via ASP.NET Core static files middleware.

### API flow

1. Upload the document via `POST /api/pls/upload` (multipart) which stores the file in Azure Blob Storage and enqueues a workflow job.
2. Poll `GET /api/pls/{jobId}` every few seconds until `state === "Succeeded"` or `"Failed"`.
3. Render the returned asset URIs (`resultPdf`, `resultHtml`, `resultDocx`, `resultJson`) directly in the browser.

### Customisation

- Adjust `src/styles.css` to match your design system.
- Update `POLL_INTERVAL_MS` inside `src/App.tsx` to poll faster/slower.
- If you expose your API behind authentication, inject auth headers in the `fetch` calls before building.
