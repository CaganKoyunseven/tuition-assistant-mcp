using System.Net;
using TuitionAssistant.Agent.Tests.Infrastructure;

namespace TuitionAssistant.Agent.Tests;

public class ChatUiTests
{
    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/index.html", "text/html")]
    [InlineData("/app.js", "javascript")]
    [InlineData("/styles.css", "text/css")]
    public async Task StaticFile_IsServedWithoutAuthentication(string path, string contentType)
    {
        using var factory = new AgentFactory();

        var response = await factory.ClientFor(null).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(contentType, response.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task IndexPage_HasTheAssignmentTitle_AndLoadsTheScript()
    {
        using var factory = new AgentFactory();

        var html = await factory.ClientFor(null).GetStringAsync("/");

        Assert.Contains("University Tuition Assistant", html);
        Assert.Contains("app.js", html);
    }

    [Fact]
    public async Task Script_NeverUsesInnerHtml_SoModelTextCannotInjectMarkup()
    {
        using var factory = new AgentFactory();

        var js = await factory.ClientFor(null).GetStringAsync("/app.js");

        Assert.DoesNotContain("innerHTML", js);
        Assert.DoesNotContain("outerHTML", js);
        Assert.DoesNotContain("insertAdjacentHTML", js);
        Assert.DoesNotContain("localStorage", js);
        Assert.DoesNotContain("sessionStorage", js);
    }

    [Fact]
    public async Task ApiRoutes_StillRequireAuthentication()
    {
        using var factory = new AgentFactory();

        var response = await factory.ClientFor(null).GetAsync("/api/v1/agent/models");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
