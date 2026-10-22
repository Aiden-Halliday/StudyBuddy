using Amazon.S3;
using Amazon.S3.Model;

namespace StudyBuddy.Services;

public class CloudflareR2Service
{
    private readonly IAmazonS3 _s3;
    private readonly IConfiguration _configuration;

    public CloudflareR2Service(
        IAmazonS3 s3,
        IConfiguration configuration)
    {
        _s3 = s3;
        _configuration = configuration;
    }

    public async Task UploadAsync(
        Stream image,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var bucket = _configuration["CloudflareR2:BucketName"]
            ?? throw new InvalidOperationException("R2 bucket is not configured.");

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = objectKey,
            InputStream = image,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(request, cancellationToken);
    }
}
