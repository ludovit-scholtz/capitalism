namespace Api.Utilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class MasterServerHttpClientRegistration
{
    private const string ClientName = "master-server";

    public static void Add(IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // Local development can use self-signed certificates when ASPNETCORE_ENVIRONMENT=Development.
            services.AddHttpClient(ClientName).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
            return;
        }

        services.AddHttpClient(ClientName);
    }
}
