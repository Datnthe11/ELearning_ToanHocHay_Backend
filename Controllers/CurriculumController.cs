using System.Security.Claims;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELearning_ToanHocHay_Control.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CurriculumController : ControllerBase
    {
        private readonly ICurriculumService _curriculumService;

        public CurriculumController(ICurriculumService curriculumService)
        {
            _curriculumService = curriculumService;
        }
        // ===================== CREATE =====================
        [HttpPost]
        //[Authorize] // Editor / Admin
        public async Task<IActionResult> Create([FromBody] CreateCurriculumDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized("User not authenticated");

            int userId = int.Parse(userIdClaim.Value);

            var result = await _curriculumService.CreateAsync(dto, userId);

            return result.Success
                ? StatusCode(201, result)
                : BadRequest(result);
        }

        // ===================== GET ALL =====================
        [HttpGet]
        //[AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var result = await _curriculumService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("full-tree")]
        //[AllowAnonymous]
        public async Task<IActionResult> GetFullTree()
        {
            var result = await _curriculumService.GetAllAsync();
            return Ok(result);
        }

        // ===================== GET BY ID =====================
        [HttpGet("{id:int}")]
        //[AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _curriculumService.GetByIdAsync(id);

            return result.Success
                ? Ok(result)
                : NotFound(result);
        }

        // ===================== UPDATE =====================
        [HttpPut("{id:int}")]
        //[Authorize] // Editor / Admin
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCurriculumDto dto)
        {
            var result = await _curriculumService.UpdateAsync(id, dto);

            return result.Success
                ? Ok(result)
                : BadRequest(result);
        }

        // ===================== DELETE =====================
        [HttpDelete("{id:int}")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _curriculumService.DeleteAsync(id);

            return result.Success
                ? Ok(result)
                : NotFound(result);
        }

        // ===================== PUBLISH =====================
        [HttpPut("{id:int}/publish")]
        //[Authorize] // Reviewer / Admin
        public async Task<IActionResult> Publish(int id)
        {
            var result = await _curriculumService.PublishAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ===================== ARCHIVE =====================
        [HttpPut("{id:int}/archive")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> Archive(int id)
        {
            var result = await _curriculumService.ArchiveAsync(id);

            return result.Success
                ? Ok(result)
                : BadRequest(result);
        }
    }
}
