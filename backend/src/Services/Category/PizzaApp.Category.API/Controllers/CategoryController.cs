using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzaApp.Category.Core.DTOs;
using PizzaApp.Category.Core.Interfaces;

namespace PizzaApp.Category.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService) => _categoryService = categoryService;

    /// <summary>Lấy tất cả danh mục pizza.</summary>
    /// <remarks>
    /// Không cần đăng nhập. App dùng API này để dựng thanh lọc danh mục ở trang chủ.
    ///
    /// Ví dụ response:
    ///
    ///     [
    ///       { "id": "c1", "name": "Truyền thống" },
    ///       { "id": "c2", "name": "Hải sản" }
    ///     ]
    /// </remarks>
    /// <response code="200">Danh sách danh mục.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll() => Ok(await _categoryService.GetAllAsync());

    /// <summary>Thống kê danh mục cho trang quản trị (chỉ Admin).</summary>
    /// <response code="200">Số liệu thống kê.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    // Phải đặt TRƯỚC GetById("{id}") - nếu không route {id} sẽ nuốt mất "admin/stats"
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Stats() => Ok(await _categoryService.GetStatsAsync());

    /// <summary>Lấy một danh mục theo Id.</summary>
    /// <param name="id">Id danh mục, ví dụ "c1".</param>
    /// <response code="200">Tìm thấy.</response>
    /// <response code="404">Không có danh mục với Id này.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category == null) return NotFound();
        return Ok(category);
    }

    /// <summary>Tạo danh mục mới (chỉ Admin).</summary>
    /// <remarks>
    /// Ví dụ request:
    ///
    ///     POST /api/category
    ///     { "name": "Pizza chay" }
    /// </remarks>
    /// <response code="200">Đã tạo, trả về danh mục vừa tạo.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CategoryDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateCategoryDTO dto) => Ok(await _categoryService.CreateAsync(dto));

    /// <summary>Đổi tên danh mục (chỉ Admin).</summary>
    /// <param name="id">Id danh mục cần sửa.</param>
    /// <param name="dto">Tên mới.</param>
    /// <response code="204">Sửa thành công.</response>
    /// <response code="404">Không tìm thấy danh mục.</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, UpdateCategoryDTO dto)
    {
        var result = await _categoryService.UpdateAsync(id, dto);
        if (!result) return NotFound();
        return NoContent();
    }

    /// <summary>Xoá danh mục (chỉ Admin).</summary>
    /// <param name="id">Id danh mục cần xoá.</param>
    /// <response code="204">Đã xoá.</response>
    /// <response code="404">Không tìm thấy danh mục.</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _categoryService.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }
}
