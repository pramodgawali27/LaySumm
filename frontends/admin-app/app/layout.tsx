import './globals.css';
import type { Metadata } from 'next';
import { ReactNode } from 'react';

import { Providers } from './providers';

export const metadata: Metadata = {
  title: 'LaySumm Admin',
  description: 'Administrative console for Plain Language Summarization platform'
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-slate-950 text-slate-50 antialiased">
        <Providers>
          <div className="min-h-screen">
            <header className="border-b border-slate-800 px-6 py-4">
              <h1 className="text-xl font-semibold">LaySumm Admin</h1>
              <p className="text-sm text-slate-300">Monitor pipelines, review summaries, and manage policies.</p>
            </header>
            <main className="p-6">{children}</main>
          </div>
        </Providers>
      </body>
    </html>
  );
}
