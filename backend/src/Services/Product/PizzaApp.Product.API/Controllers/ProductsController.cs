using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzaApp.Product.Core.DTOs;
using PizzaApp.Product.Core.Interfaces;

namespace PizzaApp.Product.API.Controllers;

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

    /// <summary>Lấy danh sách sản phẩm, có tìm kiếm, lọc theo danh mục và phân trang.</summary>
    /// <param name="search">Tìm theo tên sản phẩm. Bỏ trống = lấy tất cả.</param>
    /// <param name="categoryId">Lọc theo danh mục, ví dụ "c1". Bỏ trống = mọi danh mục.</param>
    /// <param name="page">Trang thứ mấy, bắt đầu từ 1.</param>
    /// <param name="pageSize">Số sản phẩm mỗi trang.</param>
    /// <remarks>
    /// Ví dụ: `GET /api/products?search=margherita&amp;categoryId=c1&amp;page=1&amp;pageSize=20`
    ///
    /// Ví dụ response:
    ///
    ///     [
    ///       {
    ///         "id": "1", "name": "Margherita", "description": "Phô mai, cà chua, húng quế",
    ///         "price": 10000, "imageUrl": "/uploads/abc.jpg",
    ///         "categoryId": "c1", "categoryName": "Truyền thống", "isAvailable": true
    ///       }
    ///     ]
    /// </remarks>
    /// <response code="200">Danh sách sản phẩm.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => Ok(await _productService.GetAllAsync(search, categoryId, page, pageSize));

    /// <summary>Thống kê sản phẩm cho Dashboard admin (chỉ Admin).</summary>
    /// <response code="200">Số liệu thống kê.</response>
    /// <response code="403">Không phải Admin.</response>
    // Phải đặt TRƯỚC GetById("{id}") - nếu không route {id} sẽ nuốt mất "admin/stats"
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Stats() => Ok(await _productService.GetStatsAsync());

    /// <summary>Lấy chi tiết một sản phẩm.</summary>
    /// <param name="id">Id sản phẩm.</param>
    /// <response code="200">Tìm thấy.</response>
    /// <response code="404">Không có sản phẩm với Id này.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }

    /// <summary>Tạo sản phẩm mới (chỉ Admin).</summary>
    /// <remarks>
    /// Service gọi REST sang Category để lấy tên danh mục rồi lưu kèm vào sản phẩm
    /// (denormalized), nhờ đó hiển thị tên danh mục mà không phải gọi Category mỗi lần.
    ///
    /// Ví dụ request:
    ///
    ///     POST /api/products
    ///     {
    ///       "name": "Pepperoni", "description": "Xúc xích Ý, phô mai",
    ///       "price": 99000, "imageUrl": "/uploads/abc.jpg",
    ///       "categoryId": "c1", "isAvailable": true
    ///     }
    /// </remarks>
    /// <response code="201">Đã tạo, trả về sản phẩm vừa tạo.</response>
    /// <response code="400">Danh mục không tồn tại.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateProductDTO dto)
    {
        try
        {
            var product = await _productService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Sửa sản phẩm (chỉ Admin).</summary>
    /// <param name="id">Id sản phẩm cần sửa.</param>
    /// <param name="dto">Toàn bộ thông tin mới của sản phẩm.</param>
    /// <remarks>
    /// Mỗi lần sửa, service gọi lại Category để lấy tên danh mục mới nhất
    /// và ghi đè `CategoryName`.
    /// </remarks>
    /// <response code="204">Sửa thành công.</response>
    /// <response code="400">Danh mục không tồn tại.</response>
    /// <response code="404">Không tìm thấy sản phẩm.</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, UpdateProductDTO dto)
    {
        try
        {
            var result = await _productService.UpdateAsync(id, dto);
            if (!result) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Xoá sản phẩm (chỉ Admin).</summary>
    /// <param name="id">Id sản phẩm cần xoá.</param>
    /// <remarks>
    /// Lưu ý: đây là xoá vĩnh viễn. Các đơn hàng cũ vẫn hiển thị đầy đủ vì
    /// tên và giá đã được lưu sẵn trong đơn tại thời điểm mua.
    /// </remarks>
    /// <response code="204">Đã xoá.</response>
    /// <response code="404">Không tìm thấy sản phẩm.</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _productService.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    /// <summary>Tải ảnh sản phẩm lên (chỉ Admin).</summary>
    /// <param name="file">File ảnh, gửi dạng multipart/form-data.</param>
    /// <remarks>
    /// Chỉ nhận jpg, jpeg, png, webp, gif. Ảnh lưu vào Docker volume nên không mất khi deploy lại.
    ///
    /// Trả về đường dẫn để gán vào trường `imageUrl` của sản phẩm:
    ///
    ///     { "imageUrl": "/uploads/1de0537d01364e498bbf0d51430712b3.jpg" }
    /// </remarks>
    /// <response code="200">Tải lên thành công, trả về đường dẫn ảnh.</response>
    /// <response code="400">Chưa chọn file, hoặc định dạng không được chấp nhận.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);
        using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream);

        return Ok(new { imageUrl = $"/uploads/{fileName}" });
    }
}
