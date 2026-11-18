using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Moq;
using StudyBuddy.Services;
using Xunit;

namespace StudyBuddy.Tests.Services;

public class CloudflareR2ServiceTests
{
    private readonly Mock<IAmazonS3> _s3Mock;
    private readonly CloudflareR2Service _service;

    public CloudflareR2ServiceTests()
    {
        _s3Mock = new Mock<IAmazonS3>();

        var configurationMock = new Mock<IConfiguration>();

        configurationMock
            .Setup(c => c["CloudflareR2:BucketName"])
            .Returns("test-bucket");

        _service = new CloudflareR2Service(
            _s3Mock.Object,
            configurationMock.Object);
    }

    [Fact]
    public async Task UploadAsync_ValidRequest_UploadsToCorrectBucket()
    {
        // Arrange
        var image = new MemoryStream(new byte[] { 1, 2, 3 });

        _s3Mock
            .Setup(s => s.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        await _service.UploadAsync(
            image,
            "profile-images/user-1/test.png",
            "image/png");

        // Assert
        _s3Mock.Verify(s => s.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "profile-images/user-1/test.png" &&
                r.ContentType == "image/png"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ValidKey_DeletesCorrectObject()
    {
        // Arrange
        _s3Mock
            .Setup(s => s.DeleteObjectAsync(
                It.IsAny<DeleteObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        await _service.DeleteAsync("profile-images/user-1/test.png");

        // Assert
        _s3Mock.Verify(s => s.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "profile-images/user-1/test.png"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetObjectAsync_ValidKey_RetrievesCorrectObject()
    {
        // Arrange
        var expectedResponse = new GetObjectResponse();

        _s3Mock
            .Setup(s => s.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.GetObjectAsync(
            "profile-images/user-1/test.png");

        // Assert
        Assert.Same(expectedResponse, result);

        _s3Mock.Verify(s => s.GetObjectAsync(
            It.Is<GetObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "profile-images/user-1/test.png"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
