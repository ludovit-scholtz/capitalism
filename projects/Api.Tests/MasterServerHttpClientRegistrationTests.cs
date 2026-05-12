namespace Api.Tests;

using Api.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

public sealed class MasterServerHttpClientRegistrationTests
{
    [Fact]
    public void Add_ProductionOrStaging_DoesNotConfigureCertificateBypass()
    {
        foreach (var environmentName in new[] { "Production", "Staging" })
        {
            using var provider = BuildProvider(environmentName);
            var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get("master-server");

            var builder = new CapturingHttpMessageHandlerBuilder(provider);
            foreach (var action in options.HttpMessageHandlerBuilderActions)
            {
                action(builder);
            }

            Assert.Empty(options.HttpMessageHandlerBuilderActions);
            Assert.Null(builder.PrimaryHandler);
        }
    }

    [Fact]
    public void Add_Development_ConfiguresDangerousCertificateBypass()
    {
        using var provider = BuildProvider(Environments.Development);
        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get("master-server");

        var builder = new CapturingHttpMessageHandlerBuilder(provider);
        foreach (var action in options.HttpMessageHandlerBuilderActions)
        {
            action(builder);
        }

        var handler = Assert.IsType<HttpClientHandler>(builder.PrimaryHandler);
        Assert.Same(
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            handler.ServerCertificateCustomValidationCallback);
    }

    private static ServiceProvider BuildProvider(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        MasterServerHttpClientRegistration.Add(
            services,
            new TestHostEnvironment { EnvironmentName = environmentName });

        return services.BuildServiceProvider();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(Api);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingHttpMessageHandlerBuilder(IServiceProvider services) : HttpMessageHandlerBuilder
    {
        public override string Name { get; set; } = "master-server";

        public override HttpMessageHandler PrimaryHandler { get; set; } = null!;

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();

        public override IServiceProvider Services { get; } = services;

        public override HttpMessageHandler Build() => PrimaryHandler;
    }
}
