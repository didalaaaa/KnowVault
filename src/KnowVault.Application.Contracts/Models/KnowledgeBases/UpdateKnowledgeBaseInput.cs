using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 修改知识库入参
	/// </summary>
	/// <remarks>
	/// 继承 <see cref="EntityDto{TPrimaryKey}"/> 让知识库 ID 有统一来源，并实现 <c>IEntityDto</c>；
	/// 实测 ABP 10.5 把 <c>UpdateAsync(输入DTO)</c> 映射为 <c>PUT /api/app/knowledge-base</c>（路由**不带** <c>{id}</c>），
	/// 知识库 ID 随请求体传入——前端 <c>knowledgeBases.js</c> 的 update 因此不能把 id 拼进 URL（会 405）
	/// </remarks>
	public class UpdateKnowledgeBaseInput : EntityDto<Guid>
	{
		/// <summary>
		/// 名称（首尾空白由服务端裁剪）
		/// </summary>
		[Required]
		[StringLength(128)]
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// 描述（可选）
		/// </summary>
		[StringLength(512)]
		public string? Description { get; set; }
	}
}
