using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.User.Requests;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class UserEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task AddUser_ReturnsOk_AndSavesToDatabase()
    {
        // Arrange
        var request = new UserAddRequest("John Doe", "john@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/user/add", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserAddResponse>();
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Email, result.Email);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task AddUser_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new UserAddRequest("John Doe", "john@example.com");
        await Client.PostAsJsonAsync("/user/add", request);

        // Act - same email, different case
        var duplicateRequest = new UserAddRequest("Jane Doe", "JOHN@example.com");
        var response = await Client.PostAsJsonAsync("/user/add", duplicateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("already exists", content);
    }
}
