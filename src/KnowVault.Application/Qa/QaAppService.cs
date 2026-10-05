using System.Text.Json;
using KnowVault.Ai;
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{

	/// <summary>
	/// 问答应用服务：持久化会话/消息（含引用），并调用 Ai 层做 RAG。
	/// </summary>
	[Authorize]
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
				new Message(GuidGenerator.Create(), conversation.Id, MessageRoleEnum.User, input.Question),
				autoSave: true);

			// RAG 编排：知识库缺省回落默认知识库；租户 null 归一化为 Guid.Empty（与 Qdrant payload 一致）
			var rag = await _ragService.AnswerAsync(new RagRequest
			{
				KnowledgeBaseId = input.KnowledgeBaseId ?? KnowVaultConsts.DefaultKnowledgeBaseId,
				TenantId = CurrentTenant.Id ?? Guid.Empty,
				ConversationId = conversation.Id,
				Question = input.Question,
				TopK = input.TopK ?? 0
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
					MessageRoleEnum.Assistant,
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
