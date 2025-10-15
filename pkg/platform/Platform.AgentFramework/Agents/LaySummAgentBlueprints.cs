using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Platform.AgentFramework;

namespace Platform.AgentFramework.Agents;

public interface ILaySummAgentBlueprints
{
    AgentBlueprint Reader { get; }
    AgentBlueprint Indexer { get; }
    AgentBlueprint Retriever { get; }
    AgentBlueprint Summarizer { get; }
    AgentBlueprint Visualizer { get; }
    AgentBlueprint Assembler { get; }
    AgentBlueprint Supervisor { get; }
    AgentBlueprint HumanReview { get; }
    IReadOnlyCollection<AgentBlueprint> All { get; }
}

public sealed class LaySummAgentBlueprints : ILaySummAgentBlueprints
{
    private readonly Lazy<IReadOnlyCollection<AgentBlueprint>> _all;

    public LaySummAgentBlueprints(IOptions<PlainLanguageAgentInstructions> options)
    {
        var instructions = options.Value;
        Reader = new AgentBlueprint(
            AgentNames.Reader,
            "ReaderAgent that ingests heterogeneous medical inputs (PDF, DOCX, audio) and normalises layout metadata.",
            instructions.Reader,
            new[] { ToolNames.FileIngest, ToolNames.OcrPdf, ToolNames.ParseDocx, ToolNames.AsrTranscribe },
            new Dictionary<string, string>
            {
                ["document-metadata"] = "Provides file metadata and routing hints",
                ["safety-rules"] = "Shared PLS safety guardrails"
            });

        Indexer = new AgentBlueprint(
            AgentNames.Indexer,
            "IndexerAgent that builds embeddings, keyword, and layout aware indexes.",
            instructions.Indexer,
            new[] { ToolNames.Embed, ToolNames.VectorUpsert },
            new Dictionary<string, string>
            {
                ["document-structure"] = "Structured sections emitted by ReaderAgent"
            });

        Retriever = new AgentBlueprint(
            AgentNames.Retriever,
            "RetrieverAgent that resolves prompts to hybrid BM25 + vector spans with provenance.",
            instructions.Retriever,
            new[] { ToolNames.VectorSearch, ToolNames.KeywordSearch },
            new Dictionary<string, string>
            {
                ["prompt"] = "Optional user supplied guidance",
                ["retrieval-policies"] = "Hybrid search thresholds and layout scope"
            });

        Summarizer = new AgentBlueprint(
            AgentNames.Summarizer,
            "SummarizerAgent that produces plain-language outputs with inline citations and reading level enforcement.",
            instructions.Summarizer,
            new[] { ToolNames.Summarize, ToolNames.CitationResolve },
            new Dictionary<string, string>
            {
                ["retrieved-spans"] = "List of citation spans to ground responses",
                ["style-rules"] = "Plain language and safety policies"
            });

        Visualizer = new AgentBlueprint(
            AgentNames.Visualizer,
            "VisualizerAgent that drafts prompts for explanatory visuals and orchestrates image generation.",
            instructions.Visualizer,
            new[] { ToolNames.GenerateImage },
            new Dictionary<string, string>
            {
                ["image-policies"] = "Accessibility and provenance requirements"
            });

        Assembler = new AgentBlueprint(
            AgentNames.Assembler,
            "AssemblerAgent that composes HTML, DOCX, and JSON envelopes with full provenance mapping.",
            instructions.Assembler,
            new[] { ToolNames.RenderHtml, ToolNames.RenderDocx, ToolNames.RenderPdf },
            new Dictionary<string, string>
            {
                ["summary-pieces"] = "Section level outputs with citations and visuals",
                ["document-metadata"] = "Original headers/footers, author, version"
            });

        Supervisor = new AgentBlueprint(
            AgentNames.Supervisor,
            "SupervisorAgent that coordinates the workflow graph, handles retries, and opens human review checkpoints.",
            instructions.Supervisor,
            Array.Empty<string>(),
            new Dictionary<string, string>
            {
                ["workflow-state"] = "Checkpoint state shared across agents"
            },
            RequiresThread: false);

        HumanReview = new AgentBlueprint(
            AgentNames.HumanReview,
            "Human review pseudo-agent that holds manual approval checkpoints.",
            instructions.HumanReview,
            Array.Empty<string>(),
            new Dictionary<string, string>());

        _all = new Lazy<IReadOnlyCollection<AgentBlueprint>>(() =>
            new[] { Reader, Indexer, Retriever, Summarizer, Visualizer, Assembler, Supervisor, HumanReview });
    }

    public AgentBlueprint Reader { get; }
    public AgentBlueprint Indexer { get; }
    public AgentBlueprint Retriever { get; }
    public AgentBlueprint Summarizer { get; }
    public AgentBlueprint Visualizer { get; }
    public AgentBlueprint Assembler { get; }
    public AgentBlueprint Supervisor { get; }
    public AgentBlueprint HumanReview { get; }
    public IReadOnlyCollection<AgentBlueprint> All => _all.Value;
}

public sealed class PlainLanguageAgentInstructions
{
    public string Reader { get; init; } = DefaultInstructions.Reader;
    public string Indexer { get; init; } = DefaultInstructions.Indexer;
    public string Retriever { get; init; } = DefaultInstructions.Retriever;
    public string Summarizer { get; init; } = DefaultInstructions.Summarizer;
    public string Visualizer { get; init; } = DefaultInstructions.Visualizer;
    public string Assembler { get; init; } = DefaultInstructions.Assembler;
    public string Supervisor { get; init; } = DefaultInstructions.Supervisor;
    public string HumanReview { get; init; } = DefaultInstructions.HumanReview;
}

internal static class DefaultInstructions
{
    public const string Reader = "Follow the ReaderAgent spec: ingest PDFs, DOCX, and audio; apply OCR/ASR; normalise layout; map figures.";
    public const string Indexer = "Follow the IndexerAgent spec: build hierarchical chunks; write embeddings + keywords to Azure AI Search.";
    public const string Retriever = "Follow the RetrieverAgent spec: use hybrid retrieval; enforce section coverage; return provenance spans.";
    public const string Summarizer = "Follow the SummarizerAgent spec: produce 8-10th grade summaries with inline citations and limitations.";
    public const string Visualizer = "Follow the VisualizerAgent spec: draft DALLE prompts; ensure accessibility metadata.";
    public const string Assembler = "Follow the AssemblerAgent spec: compose HTML, DOCX, and provenance appendix; preserve headers/footers.";
    public const string Supervisor = "Coordinate workflow, manage retries, and trigger human review before release.";
    public const string HumanReview = "Represent human approval gates; record reviewer decisions.";
}
