using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;

using PizzaApp.Core.Interfaces;
using PizzaApp.Core.DTOs.Product;

namespace PizzaApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _env;

    public ProductsController(IProductService productService, IWebHostEnvironment env)
    {
        _productService = productService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => Ok(await _productService.GetAllAsync(search, categoryId, page, pageSize));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateProductDTO dto)
    {
        var product = await _productService.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, UpdateProductDTO dto)
    {
        var result = await _productService.UpdateAsync(id, dto);

        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _productService.DeleteAsync(id);

        if (!result)
            return NotFound();

        return NoContent();
    }

    // Upload ảnh sản phẩm (Admin) -> trả về đường dẫn để gán vào ImageUrl
    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Chưa chọn file ảnh." });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Chỉ chấp nhận ảnh (jpg, png, webp, gif)." });

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{System.Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);
        using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        // Đường dẫn tương đối; client ghép với host để hiển thị
        var imageUrl = $"/uploads/{fileName}";
        return Ok(new { imageUrl });
    }

}
