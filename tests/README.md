# Platform Test Suites

- `tests/e2e`: orchestrated document-to-summary scenarios. Includes `LaySumm.E2E.Tests` xUnit suite exercising ingestion → summarization → validation via the gateway.
- `tests/load`: performance and soak testing harnesses (e.g., k6 scripts) validating throughput/backpressure.
- `tests/eval`: quality benchmarking pipelines against golden medical datasets and readability baselines.

Each suite will use GitHub Actions environments and IaC outputs to target dev/stage/prod clusters.
