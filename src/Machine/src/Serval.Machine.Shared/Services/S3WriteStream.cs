namespace Serval.Machine.Shared.Services;

public class S3WriteStream(
    AmazonS3Client client,
    string key,
    string bucketName,
    string uploadId,
    ILoggerFactory loggerFactory
) : Stream
{
    private readonly AmazonS3Client _client = client;
    private readonly string _key = key;
    private readonly string _uploadId = uploadId;
    private readonly string _bucketName = bucketName;
    private readonly List<UploadPartResponse> _uploadResponses = new List<UploadPartResponse>();
    private readonly ILogger<S3WriteStream> _logger = loggerFactory.CreateLogger<S3WriteStream>();

    private readonly Stream _stream = new MemoryStream();
    private int _bytesWritten = 0;

    public const int MaxPartSize = 5 * 1024 * 1024;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        // S3 buckets can only be written to in chunks of MaxPartSize
        // therefore, break it into chunks, resetting the stream each time

        while (buffer.Length + _stream.Position > MaxPartSize)
        {
            int toWrite = MaxPartSize - (int)_stream.Position;
            await _stream.WriteAsync(buffer[..toWrite], cancellationToken);
            await UploadPartAsync(cancellationToken);
            buffer = buffer[toWrite..];
        }
        // save the remaining buffer for future calls
        await _stream.WriteAsync(buffer, cancellationToken);
    }

    private async Task UploadPartAsync(CancellationToken cancellationToken = default)
    {
        if (_stream.Length == 0)
            return;
        try
        {
            _stream.Position = 0;
            int partNumber = _uploadResponses.Count + 1;
            UploadPartRequest request =
                new()
                {
                    BucketName = _bucketName,
                    Key = _key,
                    UploadId = _uploadId,
                    PartNumber = partNumber,
                    InputStream = _stream,
                    PartSize = MaxPartSize
                };
            request.StreamTransferProgress += new EventHandler<StreamTransferProgressArgs>(
                (_, e) =>
                {
                    _logger.LogDebug(
                        "Transferred {e.TransferredBytes}/{e.TotalBytes}",
                        e.TransferredBytes,
                        e.TotalBytes
                    );
                }
            );
            UploadPartResponse response = await _client.UploadPartAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException(
                    $"Tried to upload part {partNumber} of upload {_uploadId} to {_bucketName}/{_key} but received response code {response.HttpStatusCode}"
                );
            }

            _uploadResponses.Add(response);

            _bytesWritten += MaxPartSize;
            _stream.SetLength(0);
        }
        catch (Exception e)
        {
            await AbortAsync(e);
            throw;
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        UploadPartAsync().WaitAndUnwrapException();
        try
        {
            if (disposing)
            {
                if (_uploadResponses.Count == 0)
                {
                    AbortAsync().WaitAndUnwrapException();
                    PutObjectRequest request =
                        new()
                        {
                            BucketName = _bucketName,
                            Key = _key,
                            ContentBody = ""
                        };
                    PutObjectResponse response = _client.PutObjectAsync(request).WaitAndUnwrapException();
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        throw new HttpRequestException(
                            $"Tried to upload empty file to {_bucketName}/{_key} but received response code {response.HttpStatusCode}"
                        );
                    }
                }
                else
                {
                    try
                    {
                        CompleteMultipartUploadRequest request =
                            new()
                            {
                                BucketName = _bucketName,
                                Key = _key,
                                UploadId = _uploadId
                            };
                        request.AddPartETags(_uploadResponses);
                        CompleteMultipartUploadResponse response = _client
                            .CompleteMultipartUploadAsync(request)
                            .WaitAndUnwrapException();
                        if (response.HttpStatusCode != HttpStatusCode.OK)
                        {
                            throw new HttpRequestException(
                                $"Tried to complete {_uploadId} to {_bucketName}/{_key} but received response code {response.HttpStatusCode}"
                            );
                        }
                    }
                    catch (Exception e)
                    {
                        AbortAsync(e).WaitAndUnwrapException();
                        throw;
                    }
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await UploadPartAsync();
        try
        {
            if (_uploadResponses.Count == 0)
            {
                await AbortAsync();
                PutObjectRequest request =
                    new()
                    {
                        BucketName = _bucketName,
                        Key = _key,
                        ContentBody = ""
                    };
                PutObjectResponse response = await _client.PutObjectAsync(request);
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException(
                        $"Tried to upload empty file to {_bucketName}/{_key} but received response code {response.HttpStatusCode}"
                    );
                }

                return;
            }
            try
            {
                CompleteMultipartUploadRequest request =
                    new()
                    {
                        BucketName = _bucketName,
                        Key = _key,
                        UploadId = _uploadId
                    };
                request.AddPartETags(_uploadResponses);
                CompleteMultipartUploadResponse response = await _client.CompleteMultipartUploadAsync(request);
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException(
                        $"Tried to complete {_uploadId} to {_bucketName}/{_key} but received response code {response.HttpStatusCode}"
                    );
                }
            }
            catch (Exception e)
            {
                await AbortAsync(e);
            }
        }
        finally
        {
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }

    private async Task AbortAsync(Exception? e = null)
    {
        if (e is not null)
            _logger.LogError(e, "Aborted upload {UploadId} to {BucketName}/{Key}", _uploadId, _bucketName, _key);
        AbortMultipartUploadRequest abortMPURequest =
            new()
            {
                BucketName = _bucketName,
                Key = _key,
                UploadId = _uploadId
            };
        await _client.AbortMultipartUploadAsync(abortMPURequest);
    }
}
