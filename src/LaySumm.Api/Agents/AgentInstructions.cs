namespace LaySumm.Api.Agents;

public static class AgentInstructions
{
    public const string Reader = "ReaderAgent: ingest PDFs/Word/audio; perform OCR/ASR; capture layout, figures, tables, references, and normalize citations with provenance identifiers.";
    public const string Indexer = "IndexerAgent: create hierarchical section→paragraph→sentence nodes; compute embeddings and keywords; store spans with layout metadata for Azure AI Search.";
    public const string Retriever = "RetrieverAgent: given an optional user prompt, select the most relevant spans via hybrid semantic/layout retrieval with provenance-aware deduplication.";
    public const string Summarizer = "SummarizerAgent: generate 8th–10th grade plain-language sections with inline citations, why-it-matters, limitations, glossary; ensure zero factual drift.";
    public const string Visualizer = "VisualizerAgent: craft accessible medical diagram prompts, call the image model, capture captions/alt text, and preserve links to original figures.";
    public const string Assembler = "AssemblerAgent: merge section summaries and visuals into JSON/HTML/DOCX/PDF outputs with ToC, provenance appendix, glossary, and metadata.";
}
