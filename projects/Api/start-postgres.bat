@echo off
REM Prerun script to start PostgreSQL containers for MasterApi and Api projects
REM This script ensures PostgreSQL containers are running before starting the applications

echo Checking for existing PostgreSQL containers...

REM Check if master-postgres container is running
docker ps --filter "name=master-postgres" --filter "status=running" | findstr master-postgres >nul
if %errorlevel% neq 0 (
    echo Starting master-postgres container...
    docker run -d --name master-postgres -e POSTGRES_PASSWORD=password -e POSTGRES_DB=master -p 5432:5432 postgres:15
) else (
    echo master-postgres container is already running.
)

REM Check if game-postgres container is running
docker ps --filter "name=game-postgres" --filter "status=running" | findstr game-postgres >nul
if %errorlevel% neq 0 (
    echo Starting game-postgres container...
    docker run -d --name game-postgres -e POSTGRES_PASSWORD=password -e POSTGRES_DB=capitalism -p 5433:5432 postgres:15
) else (
    echo game-postgres container is already running.
)

echo PostgreSQL containers are ready.
pause