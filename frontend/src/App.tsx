import type { FormEvent } from 'react';
import { useEffect, useMemo, useState } from 'react';

type WorkflowState = 'Pending' | 'Running' | 'Succeeded' | 'Failed' | string;

interface WorkflowStatusResponse {
  jobId: string;
  state: WorkflowState;
  message?: string | null;
  resultJson?: string | null;
  resultHtml?: string | null;
  resultDocx?: string | null;
  resultPdf?: string | null;
}

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? '';
const POLL_INTERVAL_MS = 4000;

const apiUrl = (path: string) => `${API_BASE_URL}${path}`;

function App() {
  const [file, setFile] = useState<File | null>(null);
  const [prompt, setPrompt] = useState('');
  const [forceRegenerate, setForceRegenerate] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const [status, setStatus] = useState<WorkflowStatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isPollingActive = useMemo(() => {
    if (!jobId) {
      return false;
    }

    if (!status) {
      return true;
    }

    return status.state === 'Pending' || status.state === 'Running';
  }, [jobId, status]);

  useEffect(() => {
    if (!jobId || !isPollingActive) {
      return;
    }

    let isCancelled = false;

    const poll = async () => {
      try {
        const response = await fetch(apiUrl(`/api/pls/${jobId}`), {
          headers: {
            Accept: 'application/json',
          },
        });

        if (!response.ok) {
          throw new Error(`Status check failed (${response.status})`);
        }

        const payload = (await response.json()) as WorkflowStatusResponse;
        if (!isCancelled) {
          setStatus(payload);
        }
      } catch (err) {
        if (!isCancelled) {
          console.error(err);
          setError('Unable to refresh job status. Retrying…');
        }
      }
    };

    poll();
    const interval = window.setInterval(poll, POLL_INTERVAL_MS);

    return () => {
      isCancelled = true;
      window.clearInterval(interval);
    };
  }, [jobId, isPollingActive]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (!file) {
      setError('Please choose a document before submitting.');
      return;
    }

    const formData = new FormData();
    formData.append('file', file);
    if (prompt.trim().length > 0) {
      formData.append('prompt', prompt.trim());
    }
    formData.append('forceRegenerate', String(forceRegenerate));

    setIsSubmitting(true);
    setStatus(null);

    try {
      const response = await fetch(apiUrl('/api/pls/upload'), {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        const message = await parseErrorMessage(response);
        throw new Error(message ?? `Upload failed (${response.status})`);
      }

      const payload = (await response.json()) as { jobId: string };
      setJobId(payload.jobId);
    } catch (err) {
      console.error(err);
      setError(err instanceof Error ? err.message : 'Unexpected error while submitting the job.');
      setJobId(null);
    } finally {
      setIsSubmitting(false);
    }
  };

  const resetForm = () => {
    setFile(null);
    setPrompt('');
    setForceRegenerate(false);
    setJobId(null);
    setStatus(null);
    setError(null);
  };

  return (
    <main className="app-shell">
      <header>
        <h1>LaySumm – Plain Language Summary</h1>
        <p>Upload a research document, optionally provide guidance, and generate a plain-language summary.</p>
      </header>

      <section className="card">
        <form onSubmit={handleSubmit} className="form-grid">
          <label className="form-control">
            <span className="label">Document</span>
            <input
              type="file"
              accept=".pdf,.doc,.docx,.txt,.mp3,.wav,.m4a"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
              disabled={isSubmitting || isPollingActive}
              required
            />
            {file ? <small className="hint">{file.name}</small> : <small className="hint">Supported files: PDF, Word, text, audio.</small>}
          </label>

          <label className="form-control">
            <span className="label">Prompt (optional)</span>
            <textarea
              value={prompt}
              onChange={(event) => setPrompt(event.target.value)}
              placeholder="Highlight what you want the summary to focus on."
              rows={4}
              disabled={isSubmitting || isPollingActive}
            />
          </label>

          <label className="checkbox">
            <input
              type="checkbox"
              checked={forceRegenerate}
              onChange={(event) => setForceRegenerate(event.target.checked)}
              disabled={isSubmitting || isPollingActive}
            />
            <span>Force regenerate even if a summary already exists</span>
          </label>

          <div className="actions">
            <button type="submit" disabled={isSubmitting || isPollingActive}>
              {isSubmitting ? 'Uploading…' : jobId && isPollingActive ? 'Processing…' : 'Generate summary'}
            </button>
            <button type="button" className="secondary" onClick={resetForm} disabled={isSubmitting && !jobId}>
              Reset
            </button>
          </div>
        </form>

        {error && <div className="alert error">{error}</div>}

        {jobId && (
          <div className="status">
            <div className={`badge badge-${status?.state?.toLowerCase() ?? 'pending'}`}>
              {status?.state ?? 'Pending'}
            </div>
            <div>
              <h2>Job {jobId}</h2>
              <p>{status?.message ?? 'Waiting for the workflow to start.'}</p>
            </div>
          </div>
        )}
      </section>

      {status?.state === 'Succeeded' && (
        <section className="card results">
          <h2>Summary ready</h2>
          <div className="download-links">
            {status.resultPdf && (
              <a href={status.resultPdf} target="_blank" rel="noreferrer">
                PDF
              </a>
            )}
            {status.resultHtml && (
              <a href={status.resultHtml} target="_blank" rel="noreferrer">
                HTML
              </a>
            )}
            {status.resultDocx && (
              <a href={status.resultDocx} target="_blank" rel="noreferrer">
                DOCX
              </a>
            )}
            {status.resultJson && (
              <a href={status.resultJson} target="_blank" rel="noreferrer">
                JSON
              </a>
            )}
          </div>

          {status.resultPdf ? (
            <iframe title="Plain language summary PDF" src={status.resultPdf} className="pdf-frame" />
          ) : (
            <p className="placeholder">
              PDF rendering is not available yet. Download one of the generated files above to review the summary.
            </p>
          )}
        </section>
      )}

      {status?.state === 'Failed' && (
        <section className="card alert error">
          <h2>Summary failed</h2>
          <p>{status.message ?? 'Something went wrong while generating the summary.'}</p>
        </section>
      )}
    </main>
  );
}

async function parseErrorMessage(response: Response): Promise<string | null> {
  const contentType = response.headers.get('content-type');
  if (contentType?.includes('application/json')) {
    try {
      const payload = await response.json();
      if (typeof payload === 'object' && payload !== null) {
        if ('message' in payload && typeof payload.message === 'string') {
          return payload.message;
        }
        if ('title' in payload && typeof payload.title === 'string') {
          return payload.title;
        }
      }
    } catch {
      // ignore
    }
  } else {
    const text = await response.text();
    if (text) {
      return text;
    }
  }
  return null;
}

export default App;
