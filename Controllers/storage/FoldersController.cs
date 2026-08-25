using System;
using System.Collections.Generic;
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
    [Route("api/folders")]
    public class FoldersController : ControllerBase
    {
        private readonly IFolderService _folderService;

        public FoldersController(IFolderService folderService)
        {
            _folderService = folderService;
        }

        /// <summary>Crea una carpeta nueva (raíz o dentro de otra).</summary>
        [HttpPost]
        public async Task<ActionResult<FolderResponse>> CreateFolder(
            [FromBody] CreateFolderRequest request,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            var folder = await _folderService.CreateFolderAsync(tenantId, userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetFolderById), new { folderId = folder.Id }, folder);
        }

        /// <summary>Lista carpetas del tenant, opcionalmente filtradas por carpeta padre.</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FolderResponse>>> GetFolders(
            [FromQuery] Guid? parentFolderId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var folders = await _folderService.GetFoldersAsync(tenantId, parentFolderId, cancellationToken);
            return Ok(folders);
        }

        /// <summary>Obtiene una carpeta por id.</summary>
        [HttpGet("{folderId:guid}")]
        public async Task<ActionResult<FolderResponse>> GetFolderById(
            Guid folderId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var folder = await _folderService.GetFolderByIdAsync(folderId, tenantId, cancellationToken);
            return folder is null ? NotFound() : Ok(folder);
        }

        /// <summary>Renombra una carpeta.</summary>
        [HttpPut("{folderId:guid}/rename")]
        public async Task<ActionResult<FolderResponse>> RenameFolder(
            Guid folderId,
            [FromBody] RenameFolderRequest request,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var folder = await _folderService.RenameFolderAsync(folderId, tenantId, request, cancellationToken);
            return Ok(folder);
        }

        /// <summary>Elimina una carpeta.</summary>
        [HttpDelete("{folderId:guid}")]
        public async Task<IActionResult> DeleteFolder(
            Guid folderId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var deleted = await _folderService.DeleteFolderAsync(folderId, tenantId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>Lista las carpetas raíz del tenant.</summary>
        [HttpGet("root")]
        public async Task<ActionResult<IReadOnlyList<FolderResponse>>> GetRootFolders(
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var folders = await _folderService.GetRootFoldersAsync(tenantId, cancellationToken);
            return Ok(folders);
        }

        /// <summary>Lista las subcarpetas directas de una carpeta.</summary>
        [HttpGet("{parentFolderId:guid}/subfolders")]
        public async Task<ActionResult<IReadOnlyList<FolderResponse>>> GetSubFolders(
            Guid parentFolderId,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var folders = await _folderService.GetSubFoldersAsync(parentFolderId, tenantId, cancellationToken);
            return Ok(folders);
        }
    }
}