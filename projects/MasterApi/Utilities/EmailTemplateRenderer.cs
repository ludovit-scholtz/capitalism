using HandlebarsDotNet;
using Microsoft.Extensions.FileProviders;

namespace MasterApi.Utilities;

public sealed record EmailTemplateModel(
    string Locale,
    string Subject,
    string Headline,
    string BodyHtml,
    string FooterText);

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(EmailTemplateModel model, CancellationToken cancellationToken);
}

public sealed class HandlebarsEmailTemplateRenderer(IWebHostEnvironment environment) : IEmailTemplateRenderer
{
    private const string TemplatePath = "EmailTemplates/capitalism-email.html";
    private HandlebarsTemplate<object, object>? _compiledTemplate;

    public async Task<string> RenderAsync(EmailTemplateModel model, CancellationToken cancellationToken)
    {
        var template = _compiledTemplate ??= Handlebars.Compile(await ReadTemplateAsync(cancellationToken));
        return template(new
        {
            locale = model.Locale,
            subject = model.Subject,
            headline = model.Headline,
            bodyHtml = model.BodyHtml,
            footerText = model.FooterText,
        });
    }

    private async Task<string> ReadTemplateAsync(CancellationToken cancellationToken)
    {
        var file = environment.ContentRootFileProvider.GetFileInfo(TemplatePath);
        if (!file.Exists)
        {
            var physicalProvider = new PhysicalFileProvider(environment.ContentRootPath);
            file = physicalProvider.GetFileInfo(TemplatePath);
        }

        if (!file.Exists)
        {
            throw new FileNotFoundException("Email template file was not found.", TemplatePath);
        }

        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
