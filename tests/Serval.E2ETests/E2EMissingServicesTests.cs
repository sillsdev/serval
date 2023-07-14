namespace Serval.E2ETests
{
    [TestFixture]
    [Category("E2E")]
    public class E2EMissingServicesTests
    {
        [SetUp]
        public void Setup() { }

        [Test]
        public async Task UseMongoAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseEngineServerAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseJobServerAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseClearMLAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseAuth0Async()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseMissingMongoAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseMissingEngineServerAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseMissingJobServerAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseMissingClearMLAsync()
        {
            Assert.Fail();
        }

        [Test]
        public async Task UseMissingAuth0Async()
        {
            Assert.Fail();
        }

        [TearDown]
        public void TearDown() { }
    }
}
