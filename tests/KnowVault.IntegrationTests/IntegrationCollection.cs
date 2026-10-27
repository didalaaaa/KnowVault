namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 集成测试共享同一套容器：多个测试类串行执行，避免静态 <see cref="TestEnvironment.Current"/> 相互覆盖
	/// </summary>
	[CollectionDefinition(Name)]
	public sealed class IntegrationCollection : ICollectionFixture<TestEnvironment>
	{
		/// <summary>
		/// 集合名称
		/// </summary>
		public const string Name = "KnowVault integration";
	}
}
