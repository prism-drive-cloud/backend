# Implementation Plan

## 1. Dockerfile — .NET Application

* **Base image:** `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime)
* **Build stage:** `mcr.microsoft.com/dotnet/sdk:10.0`
* **Working directory:** `/app`
* **Exposed port:** `8080` (standard for .NET in containers)
* **Entry point:** `dotnet miniDriveBackend.dll`

## 2. docker-compose.yml — Orchestration

```yaml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: minidrive
      POSTGRES_USER: minidrive_user
      POSTGRES_PASSWORD: minidrive_pass
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/schema.sql:/docker-entrypoint-initdb.d/01-schema.sql
      - ./scripts/seed_data.sql:/docker-entrypoint-initdb.d/02-seed_data.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U minidrive_user -d minidrive"]
      interval: 5s
      timeout: 5s
      retries: 5
    ports:
      - "5432:5432" # Optional: for external DB tools

  api:
    build: .
    ports:
      - "8080:8080" # Expose API port
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=db;Database=minidrive;Username=minidrive_user;Password=minidrive_pass"
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

volumes:
  postgres_data:
```

## 3. appSettings Updates

Add the connection string to:

* `appsettings.json`
* `appsettings.Development.json`

Use environment variable substitution when running the application with Docker.

## 4. .dockerignore

Exclude the following files and directories:

```text
bin/
obj/
.git/
*.md
docker-compose.yml
Dockerfile
```

## Key Design Decisions

| Aspect                   | Decision                                                                 |
| ------------------------ | ------------------------------------------------------------------------ |
| DB initialization        | PostgreSQL initializes using `schema.sql` and `seed_data.sql`            |
| Healthcheck              | `pg_isready` verifies that PostgreSQL is ready                           |
| Port                     | API exposed on `8080`; PostgreSQL on `5432`                              |
| Network                  | Docker Compose provides an internal network between the API and database |
| Volumes                  | `postgres_data` persists PostgreSQL data                                 |
| Single command execution | `docker-compose up --build`                                              |

## Single Command Execution

Run:

```bash
docker-compose up --build
```

This command will:

1. Build the .NET application image.
2. Start PostgreSQL with the schema and seed data.
3. Wait until the database passes its healthcheck.
4. Start the API on port `8080`.
5. Run the complete application stack with a single command.

