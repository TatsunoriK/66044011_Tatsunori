using System.ComponentModel.DataAnnotations;

namespace _66044011_Tatsunori.ViewModels;

public class LabStudentViewModels
{
    [Required(ErrorMessage = "Student ID is required")]
    [StringLength(10)]
    [Display(Name = "Student ID")]
    public string StdID { get; set; } = null!;

    [DataType(DataType.Password)]
    [StringLength(30, MinimumLength = 6)]
    [Display(Name = "Password")]
    public string? StdPASSWORD { get; set; }

    [Display(Name = "First Name")]
    public string? StdName { get; set; }

    [Display(Name = "Last Name")]
    public string? StdLastname { get; set; }
}