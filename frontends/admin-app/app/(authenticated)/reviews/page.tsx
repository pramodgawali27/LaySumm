'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

interface ReviewTask {
  id: string;
  documentId: string;
  lane: string;
  status: 'pending' | 'in-progress' | 'completed';
  updatedAt: string;
  priority: 'low' | 'medium' | 'high';
}

export default function ReviewQueuePage() {
  const { data } = useQuery<ReviewTask[]>({
    queryKey: ['review-queue'],
    queryFn: async () => {
      // Placeholder: replace with gateway-bff fetch call
      return [
        {
          id: 'task-123',
          documentId: 'doc-001',
          lane: 'A4',
          status: 'pending',
          updatedAt: new Date().toISOString(),
          priority: 'high'
        }
      ];
    }
  });

  const grouped = useMemo(() => {
    const groups: Record<string, ReviewTask[]> = { pending: [], 'in-progress': [], completed: [] };
    (data ?? []).forEach((task) => groups[task.status].push(task));
    return groups;
  }, [data]);

  return (
    <section className="space-y-6">
      <header>
        <h2 className="text-2xl font-semibold">Human review queue</h2>
        <p className="text-sm text-slate-300">
          Prioritized list of summaries awaiting approval, grouped by workflow status.
        </p>
      </header>
      <div className="grid gap-6 md:grid-cols-3">
        {Object.entries(grouped).map(([status, tasks]) => (
          <div key={status} className="rounded border border-slate-800 bg-slate-900/60 p-4">
            <h3 className="mb-2 text-lg font-medium capitalize">{status.replace('-', ' ')}</h3>
            <ul className="space-y-2 text-sm">
              {tasks.length === 0 && <li className="text-slate-500">No tasks</li>}
              {tasks.map((task) => (
                <li key={task.id} className="flex flex-col gap-1 rounded border border-slate-800/60 p-3">
                  <span className="font-semibold">{task.documentId}</span>
                  <span className="text-xs text-slate-400">Lane {task.lane} · Priority {task.priority}</span>
                  <span className="text-xs text-slate-500">Updated {new Date(task.updatedAt).toLocaleString()}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}
