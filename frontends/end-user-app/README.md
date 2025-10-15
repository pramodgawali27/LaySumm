# End-User Frontend

## Purpose
Public-facing Next.js application that allows clinicians/patients to upload documents, track processing status, and download summaries with citations.

## Key Screens
- **Landing**: orient users and link to upload flow.
- **Upload**: collects documents, calls ingestion via gateway-bff, and toggles privacy options (to be wired up).
- **Status**: polls orchestration pipeline for readiness and reveals summary download links when complete.

## Development
```bash
pnpm install
pnpm dev
```

Environment variables:
- `NEXT_PUBLIC_GATEWAY_URL`
- `NEXT_PUBLIC_SUPPORT_EMAIL`
- `NEXT_PUBLIC_PRIVACY_POLICY_URL`

`app/upload/page.tsx` and `app/status/[id]/page.tsx` contain the required stubs that model the UX flow and highlight integration points with the gateway-bff and batch orchestration lanes.
