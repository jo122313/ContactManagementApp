using System.ComponentModel.DataAnnotations;

namespace ContactManagerApplication.Models
{
    public class SendInvitationViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
