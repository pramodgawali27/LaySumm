# 🧠 LaySumm Plain-Language Summary Service

[![MIT License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

LaySumm is now a **.NET 8** reference implementation of a plain-language summary (PLS) generator for complex medical content. The service follows the Microsoft Agent Framework (public preview) specification and produces faithful, patient-friendly summaries with provenance, visuals, and multi-format outputs.

For a detailed walkthrough of how a PDF travels through the agent workflow, see [LaySumm Technical Flow (PDF Example)](docs/technical-flow.md).

---

## ✨ Key Capabilities

- 📥 **Ingest multi-modal sources** (PDF, DOCX, audio) with OCR/ASR pipelines and structural understanding.
- 🔍 **Hybrid retrieval** that combines hierarchical chunking, semantic search, and layout-aware ranking.
- 📝 **Guardrailed summarization** tuned for 8th–10th grade reading levels with inline citations and limitations.
- 🖼️ **Visual preservation & generation** that references existing figures and emits prompts for new explanatory imagery.
- 📄 **Multi-format outputs** (JSON envelope + rendered HTML/DOCX/PDF) with a provenance appendix and glossary.
- 🔁 **Workflow supervision** with checkpoints and hooks for human approval prior to final rendering.

---

## 🏗️ Solution Architecture

```text
┌─────────────────────────┐       ┌─────────────────────┐
│   ASP.NET Core Minimal   │  POST │  /api/pls           │
│   API + Background Jobs  │──────▶│  /api/pls/{jobId}   │
└────────────┬────────────┘       └─────────┬───────────┘
             │                                │
             ▼                                ▼
┌─────────────────────────┐       ┌─────────────────────────┐
│  WorkflowDispatcher     │◀────▶│  BackgroundWorkflowQueue │
└────────────┬────────────┘       └─────────────────────────┘
             │
             ▼
┌───────────────────────────────────────────────────────────┐
│  SupervisorWorkflow                                        │
│  Reader → Indexer → (Retriever | SectionIterator) →        │
│  Summarizer → Visualizer → Assembler                       │
└───────────────────────────────────────────────────────────┘
```

Each agent is represented by a `ChatClientAgent` configured through the Microsoft Agent Framework. Azure OpenAI is the default model provider with optional OpenAI fallback. Azure Blob Storage persists source files and generated assets, Azure Table/SQL keeps provenance indexes, and Azure AI Search powers hybrid retrieval.

---

## 📁 Repository Layout

```bash
LaySumm/
├── LaySumm.sln
├── README.md
└── src/
    └── LaySumm.Api/
        ├── appsettings.json          # Configuration placeholders
        ├── LaySumm.Api.csproj        # .NET 8 minimal API project
        ├── Program.cs                # Service registration & endpoints
        ├── Agents/                   # Agent specifications & prompts
        ├── Configuration/            # Options bound from configuration
        ├── Models/                   # API request/response contracts
        └── Services/                 # Workflow orchestration components
```

---

## 🚀 Getting Started

1. **Restore & build**

   ```bash
   dotnet restore
   dotnet build
   ```

2. **Configure secrets** in `appsettings.json` or with environment variables for Azure OpenAI, Blob Storage, Azure AI Search, and image generation (`ImageGeneration` section for OpenAI DALL·E or Azure OpenAI Images).

3. **Run the API**

   ```bash
   dotnet run --project src/LaySumm.Api/LaySumm.Api.csproj
   ```

4. **Submit a job**

   ```bash
   curl -X POST https://localhost:5001/api/pls \
     -H "Content-Type: application/json" \
     -d '{
           "documents": [
             {
               "blobUri": "https://storage/.../trial.pdf",
               "fileName": "trial.pdf",
               "mediaType": "application/pdf"
             }
           ],
           "userPrompt": "Explain the primary endpoint results"
         }'
   ```

5. **Poll for status**

   ```bash
   curl https://localhost:5001/api/pls/{jobId}
   ```

---

## 📌 Next Steps

- Implement concrete Azure/OpenAI `IChatClient` adapters and workflow activities.
- Replace placeholder storage URIs with actual Azure Blob uploads.
- Integrate Azure Document Intelligence / Tesseract for OCR and Whisper/Azure Speech for ASR.
- Expand quality checks (readability scoring, contradiction detection) before final render.

---

## 📄 License

This project is released under the [MIT License](LICENSE).
