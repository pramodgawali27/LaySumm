'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function UploadPage() {
  const [file, setFile] = useState<File | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file) {
      setError('Select a PDF or DOCX file.');
      return;
    }
    setIsSubmitting(true);
    setError(null);

    try {
      // TODO: call gateway-bff ingestion endpoint
      await new Promise((resolve) => setTimeout(resolve, 800));
      router.push(`/status/${encodeURIComponent(file.name)}?queued=true`);
    } catch (err) {
      console.error(err);
      setError('Upload failed. Try again later.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form className="space-y-6" onSubmit={handleSubmit}>
      <header className="space-y-2">
        <h2 className="text-xl font-semibold">Upload a document</h2>
        <p className="text-sm text-slate-600">
          Files are encrypted at rest and processed with privacy-by-default redaction.
        </p>
      </header>
      <input
        type="file"
        accept=".pdf,.doc,.docx"
        onChange={(event) => setFile(event.target.files?.[0] ?? null)}
        className="block w-full rounded border border-slate-300 px-3 py-2"
      />
      {error && <p className="text-sm text-red-600">{error}</p>}
      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {isSubmitting ? 'Submitting…' : 'Submit'}
      </button>
    </form>
  );
}
