﻿using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPM_PR_LIB;
using RPM_Project_Backend.Helpers;
using RPM_Project_Backend.Models;
using RPM_Project_Backend.Services.Database;

namespace RPM_Project_Backend.Controllers;

/// <inheritdoc />
[ApiController]
[ApiVersion("1.0")]
[Route("resources/image")]
public class ImagesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationContext _context;
    private readonly DbSet<Image> _dbSet;

    /// <inheritdoc />
    public ImagesController(
        IConfiguration configuration, ILogger<AccountController> logger, ApplicationContext context, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _context = context;
        _environment = environment;
        _dbSet = _context.Set<Image>();
    }

    /// <summary>
    /// Get single image by id
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// GET api/image/4
    ///
    /// </remarks>
    /// <param name="id"></param>
    /// <response code="200">Return image as file</response>
    /// <response code="404">Image not found</response>
    /// <response code="500">Oops! Server internal error</response>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Image), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<Image>> Get(int id)
    {
        if (id <= 0)
            return BadRequest(new ErrorModel("Input data is empty"));
        
        _logger.LogDebug("Get image with id = {id}", id);

        var image = await _dbSet.FirstOrDefaultAsync(i => i.Id == id);
        
        if (image is null)
            return NotFound(new ErrorModel("Image not found"));
        
        var imageData = await System.IO.File.ReadAllBytesAsync(image.ImagePath);

        return File(imageData, image.ContentType);
    }
    
    /// <summary>
    /// Get single image by filename
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// GET api/image/4
    ///
    /// </remarks>
    /// <param name="filename"></param>
    /// <response code="200">Return image as file</response>
    /// <response code="404">Image not found</response>
    /// <response code="500">Oops! Server internal error</response>
    [AllowAnonymous]
    [HttpGet("{filename}")]
    [ProducesResponseType(typeof(Image), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<Image>> Get(string filename)
    {
        if (filename is null or {Length: 0})
            return BadRequest(new ErrorModel("Input data is empty"));
        
        _logger.LogDebug("Get image with filename = {filename}", filename);
        
        var image = await _dbSet.FirstOrDefaultAsync(i => i.ImagePath.Contains(filename));
        
        if (image is null)
            return NotFound(new ErrorModel("Image not found"));
        
        var imageData = await System.IO.File.ReadAllBytesAsync(image.ImagePath);

        return File(imageData, image.ContentType);
    }

    /// <summary>
    /// Upload single image
    /// </summary>
    /// <remarks>
    /// Example request
    ///
    /// POST /resources/images/upload&#xA;&#xD;
    ///
    /// Form data:&#xA;&#xD;
    ///
    /// resourceId:2
    /// type:1
    /// description:Some description
    /// image:[image file]
    /// 
    /// </remarks>
    /// <param name="imageDto"></param>
    /// <response code="200">Return image as created object</response>
    /// <response code="400">Input data is empty</response>
    /// <response code="500">Oops! Server internal error</response>
    /// <exception cref="InvalidOperationException"></exception>
    [HttpPost]
    [Authorize]
    [Route("upload")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [ProducesResponseType(typeof(Image), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<Image>> Upload([FromForm]ImageDto imageDto)
    {
        if (imageDto is null or {ResourceId: <= 0} or {Type: <= 0} or {Image: null})
            return BadRequest(new ErrorModel("Input data is empty"));
        
        var uniqueFileName = FileNameHelper.GetUniqueFileName(imageDto.Image.FileName);
        
        var filePath = Path.Combine(_environment.WebRootPath,
            "images",
            uniqueFileName);

        _logger.LogDebug("Upload image with path = {name}", filePath);
        
        var image = new Image
        {
            ResourceId = imageDto.ResourceId,
            Type = imageDto.Type,
            ContentType = imageDto.Image.ContentType,
            Description = imageDto.Description,
            Ts = DateTime.Now,
            ImagePath = filePath
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Directory name is null"));
            
            _dbSet.Add(image);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred"));
        }
        finally
        {
            await using var stream = new FileStream(filePath, FileMode.CreateNew);
            await imageDto.Image.CopyToAsync(stream);
        }

        return await _context.SaveChangesAsync() switch
        {
            0 => StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred")),
            _ => Ok(image)
        };
    }
    
    /// <summary>
    /// Delete single image by id
    /// </summary>
    /// <remarks>
    /// Example request
    ///
    /// DELETE /resources/images/delete/2
    /// 
    /// </remarks>
    /// <param name="id"></param>
    /// <response code="204">Deleted successful</response>
    /// <response code="400">Input data is empty</response>
    /// <response code="500">Oops! Server internal error</response>
    [HttpDelete("delete/{id:int}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(void), (int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Delete(int id)
    {
        if (id <= 0)
            return BadRequest(new ErrorModel("Input data is empty"));
        
        _logger.LogDebug("Delete image with id = {id}", id);

        var image = await _dbSet.FirstOrDefaultAsync(i => i.Id == id);
        
        if (image is null)
            return NotFound(new ErrorModel("Image not found"));
        
        if (System.IO.File.Exists(image.ImagePath))
        {
            System.IO.File.Delete(image.ImagePath);
        }
        else
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred"));
        }
        
        _dbSet.Remove(image);

        return await _context.SaveChangesAsync() switch
        {
            0 => StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred")),
            _ => Empty
        };
    }

    /// <summary>
    /// Delete single image by filename
    /// </summary>
    /// <remarks>
    /// Example request
    /// 
    /// DELETE /resources/images/delete/filename.png
    /// 
    /// </remarks>
    /// <param name="filename"></param>
    /// <response code="204">Deleted successful</response>
    /// <response code="400">Input data is empty</response>
    /// <response code="500">Oops! Server internal error</response>
    [HttpDelete("delete/{filename}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(void), (int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorModel), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Delete(string filename)
    {
        if (filename is null or {Length: 0})
            return BadRequest(new ErrorModel("Input data is empty"));
        
        _logger.LogDebug("Delete image with filename = {filename}", filename);
        
        var image = await _dbSet.FirstOrDefaultAsync(i => i.ImagePath.Contains(filename));
        
        if (image is null)
            return NotFound(new ErrorModel("Image not found"));

        if (System.IO.File.Exists(image.ImagePath))
        {
            System.IO.File.Delete(image.ImagePath);
        }
        else
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred"));
        }
        
        _dbSet.Remove(image);

        return await _context.SaveChangesAsync() switch
        {
            0 => StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorModel("Some error has occurred")),
            _ => Empty
        };
    }
}