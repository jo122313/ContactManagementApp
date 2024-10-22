using ContactManagerApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;


namespace ContactManagerApplication.Controllers
{

    public class InvitationController : Controller
    {
        private readonly IEmailSender _emailSender;

        public InvitationController(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }
     
        [HttpGet]
        public IActionResult SendInvitation()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendInvitation(SendInvitationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var subject = "You're Invited!";
            var message = "Click the link to join: <a href='https://localhost:7098/Identity/Account/Register'>Join Now</a>";

            await _emailSender.SendEmailAsync(model.Email, subject, message);

            ViewBag.Message = "Invitation sent successfully!";
            return View();
        }


    }
}
