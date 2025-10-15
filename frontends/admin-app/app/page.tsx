import Link from 'next/link';

export default function AdminHomePage() {
  return (
    <section className="space-y-4">
      <h2 className="text-lg font-semibold">Welcome</h2>
      <p className="text-sm text-slate-200">
        Use the navigation below to access review queues, registry management, and observability dashboards.
      </p>
      <nav className="flex gap-3">
        <Link className="rounded border border-slate-700 px-4 py-2 text-sm hover:border-slate-500" href="/(authenticated)/reviews">
          Review queue
        </Link>
        <Link className="rounded border border-slate-700 px-4 py-2 text-sm hover:border-slate-500" href="/registry">
          Registry policies
        </Link>
        <Link className="rounded border border-slate-700 px-4 py-2 text-sm hover:border-slate-500" href="/metrics">
          Metrics
        </Link>
      </nav>
    </section>
  );
}
