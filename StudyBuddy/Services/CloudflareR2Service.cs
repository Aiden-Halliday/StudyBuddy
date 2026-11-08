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

    private string GetBucketName()
    {
        return _configuration["CloudflareR2:BucketName"]
            ?? throw new InvalidOperationException("R2 bucket is not configured.");
    }


    public async Task UploadAsync(
        Stream image,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = GetBucketName(),
            Key = objectKey,
            InputStream = image,
            ContentType = contentType,

            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _s3.PutObjectAsync(request, cancellationToken);
    }


    public async Task<GetObjectResponse> GetObjectAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = GetBucketName(),
            Key = objectKey
        };

        return await _s3.GetObjectAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = GetBucketName(),
            Key = objectKey
        };

        await _s3.DeleteObjectAsync(request, cancellationToken);
    }
}