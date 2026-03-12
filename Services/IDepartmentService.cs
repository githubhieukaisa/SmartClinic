using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface IDepartmentService
    {
            Task<List<Department>> GetAllDepartmentsAsync();
            //Task<Department> GetDepartmentByIdAsync(int id);
            //Task<Department> CreateDepartmentAsync(Department newDepartment);
            //Task<Department> UpdateDepartmentAsync(int id, Department updatedDepartment);
            //Task<bool> DeleteDepartmentAsync(int id);
    }
}
