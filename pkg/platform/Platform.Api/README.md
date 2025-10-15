# Platform.Api Shared Library

Provides cross-cutting primitives for all microservices:
- `ValidationFilter<T>` endpoint filter enforcing `System.ComponentModel.DataAnnotations` rules.
- `ErrorResponse` and `FieldError` DTOs with trace correlation.
- Extension point for future shared middleware (authentication, caching, tracing).

Reference from services using a `ProjectReference` to ensure strong typing and consistent error contracts.
