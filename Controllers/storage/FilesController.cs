using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using miniDriveBackend.Api.Extensions;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Interfaces;

namespace miniDriveBackend.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        /// <summary>Lista los archivos del tenant actual (con filtros/paginación).</summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<FileResponse>>> GetFiles(
            [FromQuery] FileQueryParameters parameters,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var result = await _fileService.GetFilesAsync(tenantId, parameters, cancellationToken);
            return Ok(result);
        }

        /// <summary>Obtiene el detalle de un archivo por id.</summary>
        [HttpGet("{fileId:guid}")]
        public async Task<ActionResult<FileResponse>> GetFileById(
            Guid fileId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var file = await _fileService.GetFileByIdAsync(fileId, tenantId, cancellationToken);
            return file is null ? NotFound() : Ok(file);
        }

        /// <summary>Solicita una URL prefirmada para subir un archivo nuevo.</summary>
        [HttpPost("upload-url")]
        public async Task<ActionResult<UploadUrlResponse>> RequestUploadUrl(
            [FromBody] UploadUrlRequest request,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            var result = await _fileService.RequestUploadUrlAsync(tenantId, userId, request, cancellationToken);
            return Ok(result);
        }

        /// <summary>Confirma que el archivo subido a S3 quedó completo y lo registra.</summary>
        [HttpPost("confirm-upload")]
        public async Task<ActionResult<FileResponse>> ConfirmUpload(
            [FromBody] ConfirmUploadRequest request,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            var file = await _fileService.ConfirmUploadAsync(tenantId, userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetFileById), new { fileId = file.Id }, file);
        }

        /// <summary>Obtiene una URL prefirmada de descarga.</summary>
        [HttpGet("{fileId:guid}/download-url")]
        public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrl(
            Guid fileId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var result = await _fileService.GetDownloadUrlAsync(fileId, tenantId, cancellationToken);
            return Ok(result);
        }

        /// <summary>Renombra un archivo.</summary>
        [HttpPut("{fileId:guid}/rename")]
        public async Task<ActionResult<FileResponse>> Rename(
            Guid fileId,
            [FromBody] RenameFileRequest request,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var file = await _fileService.RenameAsync(fileId, tenantId, request, cancellationToken);
            return Ok(file);
        }

        /// <summary>Mueve un archivo a otra carpeta.</summary>
        [HttpPut("{fileId:guid}/move")]
        public async Task<ActionResult<FileResponse>> Move(
            Guid fileId,
            [FromBody] MoveFileRequest request,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var file = await _fileService.MoveAsync(fileId, tenantId, request, cancellationToken);
            return Ok(file);
        }

        /// <summary>Elimina (soft delete) un archivo.</summary>
        [HttpDelete("{fileId:guid}")]
        public async Task<IActionResult> SoftDelete(
            Guid fileId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var deleted = await _fileService.SoftDeleteAsync(fileId, tenantId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>Tamaño total ocupado por el tenant actual.</summary>
        [HttpGet("total-size")]
        public async Task<ActionResult<long>> GetTotalSize(CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var totalSize = await _fileService.GetTotalSizeByTenantAsync(tenantId, cancellationToken);
            return Ok(totalSize);
        }
    }
}