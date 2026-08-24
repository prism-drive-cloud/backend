# Business Layer Documentation

## Overview

The Business Layer contains the core business logic of the Mini Drive application. It sits between the API Controllers (Presentation Layer) and the Data Access Layer (Repositories), providing a clean separation of concerns.

## Architecture Position

```
┌─────────────────────────────────────┐
│         Controllers (API)           │
├─────────────────────────────────────┤
│        Business Layer               │
│  ┌──────────┬──────────┬─────────┐  │
│  │Interfaces│   DTOs   │Exceptions│  │
│  └──────────┴──────────┴─────────┘  │
├─────────────────────────────────────┤
│      Data Access Layer              │
│       (Repositories)                │
├─────────────────────────────────────┤
│         Database (PostgreSQL)       │
└─────────────────────────────────────┘
```

## Folder Structure

```
Business/
├── Interfaces/     # Service contracts (7 interfaces)
├── DTOs/           # Data Transfer Objects (5 files)
├── Exceptions/     # Custom business exceptions (9 classes)
└── Services/       # Implementations (to be created)
```

## Design Principles

1. **Interface Segregation** - Each service has a focused, single-responsibility interface
2. **Dependency Inversion** - Controllers depend on interfaces, not implementations
3. **DTO Pattern** - Data shapes for API contracts, separate from domain entities
4. **Rich Domain Exceptions** - Exceptions carry contextual data for proper HTTP responses
5. **Multi-tenancy First** - All services enforce tenant isolation at the business layer