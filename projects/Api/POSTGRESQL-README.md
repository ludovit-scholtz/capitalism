# PostgreSQL Setup for Local Development

This guide explains how to set up PostgreSQL databases for local development of the Capitalism game.

## Prerequisites

- Docker installed and running
- .NET 10 SDK

## Setup

1. Run the prerun script to start PostgreSQL containers:
   ```bash
   ./start-postgres.bat
   ```
   This will start two PostgreSQL containers:
   - `master-postgres` on port 5432 for the MasterApi
   - `game-postgres` on port 5433 for the Api (game backend)

2. The containers will be created with default databases and users.

## Configuration

The appsettings.json files have been updated to use PostgreSQL:

- `projects/Api/appsettings.json`: Uses `Host=localhost;Port=5433;Database=capitalism;Username=postgres;Password=password`
- `projects/MasterApi/appsettings.json`: Uses `Host=localhost;Port=5432;Database=master;Username=postgres;Password=password`

## Running the Applications

After starting the PostgreSQL containers, you can run the applications normally:

```bash
# Game API
cd projects/Api
dotnet run

# Master API
cd projects/MasterApi
dotnet run
```

The applications will automatically create the database schema on first run.

## Cleanup

To stop and remove the containers:
```bash
docker stop master-postgres game-postgres
docker rm master-postgres game-postgres
```