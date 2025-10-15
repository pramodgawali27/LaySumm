import './globals.css';
import type { Metadata } from 'next';
import { ReactNode } from 'react';

export const metadata: Metadata = {
  title: 'LaySumm Plain Language Summaries',
  description: 'Upload clinical documents and receive patient-friendly explanations.'
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-slate-50 text-slate-900">
        <div className="mx-auto max-w-3xl px-4 py-10">
          <header className="mb-10 text-center">
            <h1 className="text-3xl font-semibold">LaySumm</h1>
            <p className="text-sm text-slate-600">Plain-language medical summaries with traceable evidence.</p>
          </header>
          <main>{children}</main>
        </div>
      </body>
    </html>
  );
}
