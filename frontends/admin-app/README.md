# Admin Frontend

## Purpose
Next.js (App Router) administrative console used by reviewers, ML engineers, and compliance teams. Authenticated via OIDC and communicates with the gateway-bff.

## Key Screens
- **Review Queue**: triage tasks by status/priority with diff viewer hook.
- **Registry Management**: manage prompts, adapters, policies (future).
- **Observability**: embed dashboards from observability service.

## Development
```bash
pnpm install
pnpm dev
```

Environment variables:
- `NEXT_PUBLIC_OIDC_AUTHORITY`
- `NEXT_PUBLIC_GATEWAY_URL`
- `NEXT_PUBLIC_FEATURE_FLAGS`

The `app/(authenticated)/reviews/page.tsx` stub demonstrates how the React Query cache will integrate with the gateway-bff queue API.
