using System;
using System.Threading;
using System.Threading.Tasks;

namespace KnowVault.Ai.Rag
{

	/// <summary>
	/// RAG 编排入口：检索 -> 组装 Prompt -> 生成 -> 引用溯源。
	/// P0 骨架只定义契约，真实闭环 P1 接入。
	/// </summary>
	public interface IRagService
	{
	    Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default);
	}

}
