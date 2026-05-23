using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[CollectionDefinition("OrchestrationApi", DisableParallelization = true)]
public class OrchestrationApiCollection : ICollectionFixture<JoyZoningApiCollectionFixture>;
