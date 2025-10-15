import Link from 'next/link';

export default function LandingPage() {
  return (
    <section className="space-y-6">
      <h2 className="text-2xl font-semibold">Get started</h2>
      <p className="text-base text-slate-600">
        Upload a medical document to generate a patient-facing summary with citations and readability assurances.
      </p>
      <Link
        className="inline-block rounded bg-slate-900 px-5 py-3 text-sm font-medium text-white hover:bg-slate-700"
        href="/upload"
      >
        Upload a document
      </Link>
    </section>
  );
}
