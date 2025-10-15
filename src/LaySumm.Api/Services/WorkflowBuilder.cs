using LaySumm.Api.Agents;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Services;

public sealed class WorkflowBuilder
{
    private readonly AgentFactory _agentFactory;
    private readonly ILoggerFactory _loggerFactory;

    public WorkflowBuilder(AgentFactory agentFactory, ILoggerFactory loggerFactory)
    {
        _agentFactory = agentFactory;
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

        return new SupervisorWorkflow(job, agents, logger);
    }
}

public sealed record WorkflowAgents(
    AgentSpecification Reader,
    AgentSpecification Indexer,
    AgentSpecification Retriever,
    AgentSpecification Summarizer,
    AgentSpecification Visualizer,
    AgentSpecification Assembler);
