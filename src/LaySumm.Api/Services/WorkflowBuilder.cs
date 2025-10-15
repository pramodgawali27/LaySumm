using LaySumm.Api.Agents;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Services;

public sealed class WorkflowBuilder
{
    private readonly AgentFactory _agentFactory;
    private readonly Processing.Ingestion.FileIngestionService _fileIngestionService;
    private readonly Processing.Ingestion.DocumentParser _documentParser;
    private readonly Processing.Indexing.DocumentIndexer _documentIndexer;
    private readonly Processing.Retrieval.DocumentRetriever _documentRetriever;
    private readonly Processing.Summarization.SummarizationService _summarizationService;
    private readonly Processing.Visualization.VisualizationService _visualizationService;
    private readonly Processing.Assembly.DocumentAssembler _documentAssembler;
    private readonly ILoggerFactory _loggerFactory;

    public WorkflowBuilder(
        AgentFactory agentFactory,
        Processing.Ingestion.FileIngestionService fileIngestionService,
        Processing.Ingestion.DocumentParser documentParser,
        Processing.Indexing.DocumentIndexer documentIndexer,
        Processing.Retrieval.DocumentRetriever documentRetriever,
        Processing.Summarization.SummarizationService summarizationService,
        Processing.Visualization.VisualizationService visualizationService,
        Processing.Assembly.DocumentAssembler documentAssembler,
        ILoggerFactory loggerFactory)
    {
        _agentFactory = agentFactory;
        _fileIngestionService = fileIngestionService;
        _documentParser = documentParser;
        _documentIndexer = documentIndexer;
        _documentRetriever = documentRetriever;
        _summarizationService = summarizationService;
        _visualizationService = visualizationService;
        _documentAssembler = documentAssembler;
        _loggerFactory = loggerFactory;
    }

    public SupervisorWorkflow Build(WorkflowJob job)
    {
        var logger = _loggerFactory.CreateLogger<SupervisorWorkflow>();

        var agents = new WorkflowAgents(
            _agentFactory.CreateReaderAgent(),
            _agentFactory.CreateIndexerAgent(),
            _agentFactory.CreateRetrieverAgent(),
            _agentFactory.CreateSummarizerAgent(),
            _agentFactory.CreateVisualizerAgent(),
            _agentFactory.CreateAssemblerAgent());

        return new SupervisorWorkflow(
            job,
            agents,
            _fileIngestionService,
            _documentParser,
            _documentIndexer,
            _documentRetriever,
            _summarizationService,
            _visualizationService,
            _documentAssembler,
            logger);
    }
}

public sealed record WorkflowAgents(
    AgentSpecification Reader,
    AgentSpecification Indexer,
    AgentSpecification Retriever,
    AgentSpecification Summarizer,
    AgentSpecification Visualizer,
    AgentSpecification Assembler);
