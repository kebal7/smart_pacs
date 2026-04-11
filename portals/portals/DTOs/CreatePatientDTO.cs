using System.ComponentModel.DataAnnotations;

namespace portals.DTOs
{
    public class CreatePatientDto
    {
        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 100 characters")]
        [RegularExpression(@"^[a-zA-Z\s\. ]+$", ErrorMessage = "Name can only contain letters, dots, and spaces")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Date of Birth is required")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Address is required")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Contact number is required")]
        [RegularExpression(@"^(\+?\d{7,15})$", ErrorMessage = "Contact number must be 7 to 15 digits (e.g. +9779841234567)")]
        public string ContactNo { get; set; }
        
        public string EmergencyContact { get; set; }
    }
}