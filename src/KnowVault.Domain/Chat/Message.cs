using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using KnowVault.Domain.Shared.Chat;

namespace KnowVault.Domain.Chat
{

	/// <summary>
	/// 消息：含引用溯源（CitationsJson），供前端展示引用出处。
	/// </summary>
	public class Message : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
	    public Guid ConversationId { get; set; }

	    public MessageRole Role { get; set; }

	    public string Content { get; set; } = default!;

	    /// <summary>引用溯源（JSON 数组：documentId/title/chunkIndex/score），可空。</summary>
	    public string? CitationsJson { get; set; }

	    public Guid? TenantId { get; set; }

	    protected Message()
	    {
	    }

	    public Message(
	        Guid id,
	        Guid conversationId,
	        MessageRole role,
	        string content,
	        string? citationsJson = null,
	        Guid? tenantId = null) : base(id)
	    {
	        ConversationId = conversationId;
	        Role = role;
	        Content = content;
	        CitationsJson = citationsJson;
	        TenantId = tenantId;
	    }
	}

}
