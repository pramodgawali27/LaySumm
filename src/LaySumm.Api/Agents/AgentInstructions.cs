namespace LaySumm.Api.Agents;

public static class AgentInstructions
{
    public const string Reader = "Extract structure, run OCR/ASR, normalize references as per specification.";
    public const string Indexer = "Build embeddings and keyword indexes with provenance-aware layout spans.";
    public const string Retriever = "Select top-ranked spans using hierarchical chunking, semantic similarity, and layout metadata.";
    public const string Summarizer = "Produce faithful plain-language summaries with inline citations and guardrails.";
    public const string Visualizer = "Emit prompts for explanatory visuals, invoke the image model, store metadata.";
    public const string Assembler = "Compose final outputs (JSON, HTML, DOCX, PDF) with provenance mapping.";
}
