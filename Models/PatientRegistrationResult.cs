namespace SmartClinic.Models
{
    public class PatientRegistrationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public bool AddedPatientRoleToExistingUser { get; init; }
    }
}