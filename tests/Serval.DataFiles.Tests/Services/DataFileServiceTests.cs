﻿using Serval.Shared.Contracts;

namespace Serval.DataFiles.Services;

[TestFixture]
public class DataFileServiceTests
{
    [Test]
    public async Task CreateAsync_NoError()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        var dataFile = new DataFile { Name = "file2" };
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            await env.Service.CreateAsync(dataFile, stream);

        Assert.That(env.DataFiles.Contains(dataFile.Id), Is.True);
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
    }

    [Test]
    public void CreateAsync_Error()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = "file1",
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        var dataFile = new DataFile { Id = "file1", Name = "file1" };
        string content = "This is a file.";
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            Assert.ThrowsAsync<DuplicateKeyException>(() => env.Service.CreateAsync(dataFile, stream));

        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task UpdateAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = "file1",
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        DataFile? dataFile;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            dataFile = await env.Service.UpdateAsync("file1", stream);

        Assert.That(dataFile, Is.Not.Null);
        Assert.That(dataFile.Revision, Is.EqualTo(2));
        Assert.That(Encoding.UTF8.GetString(fileStream.ToArray()), Is.EqualTo(content));
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));
    }

    [Test]
    public async Task UpdateAsync_DoesNotExist()
    {
        var env = new TestEnvironment();
        using var fileStream = new MemoryStream();
        env.FileSystem.OpenWrite(Arg.Any<string>()).Returns(fileStream);
        string content = "This is a file.";
        DataFile? dataFile;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            dataFile = await env.Service.UpdateAsync("file1", stream);

        Assert.That(dataFile, Is.Null);
        env.FileSystem.Received().DeleteFile(Arg.Any<string>());
    }

    [Test]
    public async Task DeleteAsync_Exists()
    {
        var env = new TestEnvironment();
        env.DataFiles.Add(
            new DataFile
            {
                Id = "file1",
                Name = "file1",
                Filename = "file1.txt"
            }
        );
        bool deleted = await env.Service.DeleteAsync("file1");

        Assert.That(deleted, Is.True);
        Assert.That(env.DataFiles.Contains("file1"), Is.False);
        DeletedFile deletedFile = env.DeletedFiles.Entities.Single();
        Assert.That(deletedFile.Filename, Is.EqualTo("file1.txt"));
        await env.Mediator.Received().Publish(Arg.Any<DataFileDeleted>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAsync_DoesNotExist()
    {
        var env = new TestEnvironment();
        bool deleted = await env.Service.DeleteAsync("file1");

        Assert.That(deleted, Is.False);
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            DataFiles = new MemoryRepository<DataFile>();
            var options = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            options.CurrentValue.Returns(new DataFileOptions());
            Mediator = Substitute.For<IScopedMediator>();
            DeletedFiles = new MemoryRepository<DeletedFile>();
            FileSystem = Substitute.For<IFileSystem>();
            Service = new DataFileService(
                DataFiles,
                new MemoryDataAccessContext(),
                options,
                Mediator,
                DeletedFiles,
                FileSystem
            );
        }

        public IFileSystem FileSystem { get; }
        public MemoryRepository<DeletedFile> DeletedFiles { get; }
        public IScopedMediator Mediator { get; }
        public MemoryRepository<DataFile> DataFiles { get; }
        public DataFileService Service { get; }
    }
}