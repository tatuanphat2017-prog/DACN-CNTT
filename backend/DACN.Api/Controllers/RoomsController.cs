using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Phòng học (tòa, lầu). Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/rooms")]
[Authorize]
public class RoomsController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Danh sách phòng học, sắp theo tòa, lầu, mã phòng. Lọc theo tòa bằng building.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RoomDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? building)
    {
        var query = db.Rooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(building))
        {
            var b = building.Trim();
            query = query.Where(r => r.Building == b);
        }
        return Ok(await query.OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.Code)
            .Select(r => new RoomDto(r.Id, r.Code, r.Building, r.Floor, r.Capacity)).ToListAsync());
    }

    /// <summary>Thêm phòng học. Mã phòng trùng trả 409.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<RoomDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UpsertRoomRequest request)
    {
        var code = request.Code.Trim();
        if (await db.Rooms.AnyAsync(r => r.Code == code))
            return ConflictProblem("Mã phòng đã tồn tại");

        var room = new Room { Code = code, Building = request.Building.Trim(), Floor = request.Floor, Capacity = request.Capacity };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return Created($"/api/rooms/{room.Id}", ToDto(room));
    }

    /// <summary>Sửa phòng học.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpsertRoomRequest request)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room is null)
            return NotFound();
        var code = request.Code.Trim();
        if (await db.Rooms.AnyAsync(r => r.Code == code && r.Id != id))
            return ConflictProblem("Mã phòng đã tồn tại");

        room.Code = code;
        room.Building = request.Building.Trim();
        room.Floor = request.Floor;
        room.Capacity = request.Capacity;
        await db.SaveChangesAsync();
        return Ok(ToDto(room));
    }

    /// <summary>Xóa phòng học. Phòng đang được dùng cho lịch học hoặc lịch thi của lớp nào thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room is null)
            return NotFound();
        if (await db.CourseSections.AnyAsync(s => s.RoomId == id || s.ExamRoomId == id))
            return ConflictProblem("Phòng đang được dùng cho lớp học phần, không xóa được");

        db.Rooms.Remove(room);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static RoomDto ToDto(Room r) => new(r.Id, r.Code, r.Building, r.Floor, r.Capacity);
}
