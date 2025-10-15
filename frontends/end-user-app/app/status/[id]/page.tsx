'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';

interface StatusEntry {
  step: string;
  status: 'pending' | 'running' | 'completed' | 'failed';
  updatedAt: string;
}

export default function StatusPage({ params }: { params: { id: string } }) {
  const searchParams = useSearchParams();
  const [entries, setEntries] = useState<StatusEntry[]>([]);
  const [isPolling, setIsPolling] = useState(false);

  useEffect(() => {
    let isMounted = true;
    async function fetchStatus() {
      setIsPolling(true);
      try {
        // Placeholder for gateway-bff polling request
        const fakeTimeline: StatusEntry[] = [
          { step: 'Ingestion', status: 'completed', updatedAt: new Date().toISOString() },
          { step: 'PII Redaction', status: 'completed', updatedAt: new Date().toISOString() },
          { step: 'Summarization', status: 'running', updatedAt: new Date().toISOString() }
        ];
        if (isMounted) {
          setEntries(fakeTimeline);
        }
      } finally {
        if (isMounted) {
          setIsPolling(false);
        }
      }
    }

    fetchStatus();
    const interval = setInterval(fetchStatus, 5000);
    return () => {
      isMounted = false;
      clearInterval(interval);
    };
  }, []);

  const queued = searchParams.get('queued');

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <h2 className="text-xl font-semibold">Processing status</h2>
        <p className="text-sm text-slate-600">
          Tracking document <span className="font-mono">{params.id}</span>
          {queued ? ' (recently queued)' : ''}.
        </p>
      </header>
      <div className="space-y-3">
        {entries.map((entry) => (
          <div key={entry.step} className="rounded border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between">
              <h3 className="font-medium">{entry.step}</h3>
              <span className="text-xs uppercase tracking-wide text-slate-500">{entry.status}</span>
            </div>
            <p className="mt-2 text-xs text-slate-500">Updated {new Date(entry.updatedAt).toLocaleString()}</p>
          </div>
        ))}
        {entries.length === 0 && !isPolling && <p className="text-sm text-slate-500">No updates yet.</p>}
      </div>
    </section>
  );
}
