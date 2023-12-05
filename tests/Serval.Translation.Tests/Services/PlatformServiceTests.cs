using Google.Protobuf.WellKnownTypes;
using MassTransit;
using Serval.Translation.V1;
using SIL.ObjectModel;

namespace Serval.Translation.Services;

[TestFixture]
public class PlatformServiceTests
{
    [Test]
    public async Task TestBuildStateTransitionsAsync() { }

    [Test]
    public async Task UpdateBuildStatusAsync() { }

    [Test]
    public async Task IncrementCorpusSizeAsync() { }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Builds = new MemoryRepository<Build>();
            Engines = new MemoryRepository<Engine>();
            Pretranslations = new MemoryRepository<Pretranslation>();
            DataAccessContext = Substitute.For<IDataAccessContext>();
            PublishEndpoint = Substitute.For<IPublishEndpoint>();
            ServerCallContext = Substitute.For<ServerCallContext>();
        }

        public IRepository<Build> Builds { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<Pretranslation> Pretranslations { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IPublishEndpoint PublishEndpoint { get; }
        public ServerCallContext ServerCallContext { get; }
    }
}
