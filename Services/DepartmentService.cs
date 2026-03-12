using Microsoft.EntityFrameworkCore;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly SmartClinicDbContext _context;
        public DepartmentService(SmartClinicDbContext context)
        {
            _context = context;
        }

        public async Task<List<Department>> GetAllDepartmentsAsync()
        {
            return await _context.Departments.ToListAsync();
        }
    }
}
