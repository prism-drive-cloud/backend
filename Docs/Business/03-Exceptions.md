# Exceptions - Purpose and Design

## What are Custom Exceptions?

Custom exceptions are domain-specific exception types that carry **business context** beyond a simple error message. They enable structured error handling and proper HTTP response mapping.

```csharp
public class QuotaExceededException : BusinessException
{
    public long RequestedBytes { get; }
    public long AvailableBytes { get; }
    public long QuotaBytes { get; }

    public QuotaExceededException(long requested, long available, long quota)
        : base(
            $"Storage quota exceeded. Requested: {requested}, Available: {available}, Quota: {quota}",
            "QUOTA_EXCEEDED",
            400)
    {
        RequestedBytes = requested;
        AvailableBytes = available;
        QuotaBytes = quota;
    }
}
```

## Why Custom Exceptions?

### 1. **Structured Error Responses**
Instead of generic 500 errors, return meaningful HTTP status codes with typed error data:

```json
{
  "errorCode": "QUOTA_EXCEEDED",
  "message": "Storage quota exceeded. Requested: 524288000, Available: 104857600, Quota: 1073741824",
  "details": {
    "requestedBytes": 524288000,
    "availableBytes": 104857600,
    "quotaBytes": 1073741824
  }
}
```

### 2. **Catch-Specific Handling**
Controllers can catch specific exceptions without parsing messages:

```csharp
try
{
    return await _fileService.RequestUploadUrlAsync(tenantId, userId, request);
}
catch (QuotaExceededException ex)
{
    return Problem(
        title: "Quota Exceeded",
        detail: ex.Message,
        statusCode: ex.StatusCode,
        extensions: new Dictionary<string, object>
        {
            ["requestedBytes"] = ex.RequestedBytes,
            ["availableBytes"] = ex.AvailableBytes,
            ["quotaBytes"] = ex.QuotaBytes
        });
}
```

### 3. **Domain Language in Code**
Exceptions use business terminology (`QuotaExceeded`, `TenantNotFound`) instead of technical terms (`SqlException`, `ArgumentNullException`).

### 4. **Centralized Error Handling**
Global exception middleware can map exception types to HTTP responses consistently.

## Base Exception: `BusinessException`

```csharp
public abstract class BusinessException : Exception
{
    public string ErrorCode { get; }      // Machine-readable code
    public int StatusCode { get; }        // HTTP status code

    protected BusinessException(string message, string errorCode, int statusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
```

Properties:
- **ErrorCode** - Unique identifier for frontend handling (e.g., "QUOTA_EXCEEDED")
- **StatusCode** - Direct mapping to HTTP response (400, 401, 403, 404, 409, 500)
- **Message** - Human-readable description for logging/display

## Exception Catalog

| Exception | HTTP Status | Error Code | When Thrown |
|-----------|-------------|------------|-------------|
| `QuotaExceededException` | 400 | `QUOTA_EXCEEDED` | Upload would exceed 1GB tenant limit |
| `TenantNotFoundException` | 404 | `TENANT_NOT_FOUND` | Tenant ID/Slug not found |
| `UserNotFoundException` | 404 | `USER_NOT_FOUND` | User ID/Email not found |
| `FileNotFoundException` | 404 | `FILE_NOT_FOUND` | File ID not found or not in tenant |
| `FolderNotFoundException` | 404 | `FOLDER_NOT_FOUND` | Folder ID not found or not in tenant |
| `UnauthorizedAccessException` | 403 | `UNAUTHORIZED_ACCESS` | Cross-tenant access attempt |
| `InvalidCredentialsException` | 401 | `INVALID_CREDENTIALS` | Wrong email/password on login |
| `DuplicateResourceException` | 409 | `DUPLICATE_RESOURCE` | Unique constraint violation (slug, email) |
| `S3OperationException` | 500 | `S3_OPERATION_FAILED` | AWS S3 SDK errors |

## Exception Hierarchy

```
Exception (System)
└── BusinessException (abstract)
    ├── QuotaExceededException
    ├── TenantNotFoundException
    ├── UserNotFoundException
    ├── FileNotFoundException
    ├── FolderNotFoundException
    ├── UnauthorizedAccessException
    ├── InvalidCredentialsException
    ├── DuplicateResourceException
    └── S3OperationException
```

## Usage in Business Services

```csharp
public async Task<UploadUrlResponse> RequestUploadUrlAsync(...)
{
    // Validate quota
    var hasQuota = await _storageService.CheckQuotaAvailableAsync(tenantId, request.SizeBytes);
    if (!hasQuota)
    {
        var usage = await _storageService.GetStorageInfoAsync(tenantId);
        throw new QuotaExceededException(
            request.SizeBytes,
            usage.AvailableBytes,
            usage.QuotaBytes);
    }

    // Validate tenant exists
    var tenant = await _tenantRepository.GetByIdAsync(tenantId);
    if (tenant == null)
        throw new TenantNotFoundException(tenantId);

    // Generate S3 key with tenant isolation
    var s3Key = _s3Service.BuildS3Key(tenantId, userId, request.FileName);

    // Check for duplicate
    if (await _fileRepository.ExistsByS3KeyAsync(s3Key))
        throw new DuplicateResourceException("File", "S3Key", s3Key);

    // ... rest of logic
}
```

## Global Exception Handling (Middleware)

```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business error: {ErrorCode}", ex.ErrorCode);
            await WriteErrorResponse(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
            await WriteErrorResponse(context, new BusinessException(
                "An unexpected error occurred",
                "INTERNAL_ERROR",
                500));
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, BusinessException ex)
    {
        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            errorCode = ex.ErrorCode,
            message = ex.Message,
            details = GetExceptionDetails(ex)
        };

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static object? GetExceptionDetails(BusinessException ex)
    {
        return ex switch
        {
            QuotaExceededException qe => new { qe.RequestedBytes, qe.AvailableBytes, qe.QuotaBytes },
            DuplicateResourceException dr => new { dr.ResourceType, dr.Field, dr.Value },
            S3OperationException s3 => new { s3.Operation, s3.S3Key },
            UnauthorizedAccessException ua => new { ua.ResourceType, ua.ResourceId },
            _ => null
        };
    }
}
```

## Registration in Program.cs

```csharp
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

// ... rest of pipeline
```

## Anti-Patterns to Avoid

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| Throwing `Exception` or `ApplicationException` | No semantic meaning, hard to catch specifically | Use custom exceptions |
| Parsing exception messages | Fragile, breaks with message changes | Use typed properties |
| Catching `Exception` broadly | Hides bugs, prevents proper handling | Catch specific business exceptions |
| Missing `ErrorCode` | Frontend can't handle errors programmatically | Always include machine-readable code |
| Using 500 for business errors | Masks client-fixable issues | Map to 4xx for client errors |