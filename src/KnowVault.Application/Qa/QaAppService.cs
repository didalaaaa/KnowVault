using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowVault.Ai.Rag;
using KnowVault.Application.Contracts.Qa;
using KnowVault.Domain.Chat;
using KnowVault.Domain.Shared.Chat;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application.Qa
{

	/// <summary>
	/// 问答应用服务：持久化会话/消息（含引用），并调用 Ai 层做 RAG。
	/// P0 的 Ai 层返回占位回答，真实检索/生成在 P1 接入。
	/// </summary>
	public class QaAppService : KnowVaultAppService, IQaAppService
	{
	    private readonly IRagService _ragService;
	    private readonly IRepository<Conversation, Guid> _conversationRepository;
	    private readonly IRepository<Message, Guid> _messageRepository;

	    public QaAppService(
	        IRagService ragService,
	        IRepository<Conversation, Guid> conversationRepository,
	        IRepository<Message, Guid> messageRepository)
	    {
	        _ragService = ragService;
	        _conversationRepository = conversationRepository;
	        _messageRepository = messageRepository;
	    }

	    public async Task<QaAnswerDto> AskAsync(AskQuestionInput input)
	    {
	        var conversation = input.ConversationId.HasValue
	            ? await _conversationRepository.GetAsync(input.ConversationId.Value)
	            : await _conversationRepository.InsertAsync(
	                new Conversation(
	                    GuidGenerator.Create(),
	                    input.Question.Length <= 128 ? input.Question : input.Question[..128]),
	                autoSave: true);

	        // 用户消息
	        await _messageRepository.InsertAsync(
	            new Message(GuidGenerator.Create(), conversation.Id, MessageRole.User, input.Question),
	            autoSave: true);

	        // RAG 编排
	        var rag = await _ragService.AnswerAsync(new RagRequest
	        {
	            KnowledgeBaseId = input.KnowledgeBaseId ?? Guid.Empty,
	            ConversationId = conversation.Id,
	            Question = input.Question
	        });

	        var citations = rag.Citations
	            .Select(c => new RagCitationDto
	            {
	                DocumentId = c.DocumentId,
	                Title = c.Title,
	                ChunkIndex = c.ChunkIndex,
	                Score = c.Score
	            })
	            .ToList();

	        // 助手消息（含引用溯源）
	        await _messageRepository.InsertAsync(
	            new Message(
	                GuidGenerator.Create(),
	                conversation.Id,
	                MessageRole.Assistant,
	                rag.Answer,
	                citationsJson: JsonSerializer.Serialize(citations)),
	            autoSave: true);

	        return new QaAnswerDto
	        {
	            ConversationId = conversation.Id,
	            Answer = rag.Answer,
	            Citations = citations
	        };
	    }
	}

}
