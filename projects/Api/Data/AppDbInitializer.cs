using System.Data;
using Api.Configuration;
using Api.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;

namespace Api.Data;

public sealed class AppDbInitializer(
    AppDbContext dbContext,
    IOptions<SeedDataOptions> seedOptions)
{
}
