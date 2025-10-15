using LaySumm.Api.Configuration;
using Microsoft.Extensions.Options;

namespace LaySumm.Api.Agents;

public sealed class AgentFactory
{
    private readonly AzureOpenAIOptions _options;

    public AgentFactory(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;
    }

    public AgentSpecification CreateReaderAgent() => new(
        "ReaderAgent",
        AgentInstructions.Reader,
        _options.Deployment);

    public AgentSpecification CreateIndexerAgent() => new(
        "IndexerAgent",
        AgentInstructions.Indexer,
        _options.Deployment);

    public AgentSpecification CreateRetrieverAgent() => new(
        "RetrieverAgent",
        AgentInstructions.Retriever,
        _options.Deployment);

    public AgentSpecification CreateSummarizerAgent() => new(
        "SummarizerAgent",
        AgentInstructions.Summarizer,
        _options.Deployment);

    public AgentSpecification CreateVisualizerAgent() => new(
        "VisualizerAgent",
        AgentInstructions.Visualizer,
        _options.Deployment);

    public AgentSpecification CreateAssemblerAgent() => new(
        "AssemblerAgent",
        AgentInstructions.Assembler,
        _options.Deployment);
}

public sealed record AgentSpecification(string Name, string Instructions, string Deployment);
