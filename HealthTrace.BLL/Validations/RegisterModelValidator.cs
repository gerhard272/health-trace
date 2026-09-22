using FluentValidation;
using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Validations
{
    public class RegisterModelValidator : AbstractValidator<RegisterModel>
    {
        public RegisterModelValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MaximumLength(50).WithMessage("Username must not exceed 50 characters");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("FirstName is required")
                .MaximumLength(50).WithMessage("FirstName must not exceed 50 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("LastName is required")
                .MaximumLength(50).WithMessage("LastName must not exceed 50 characters");

            RuleFor(x => x.CF)
                .NotEmpty().WithMessage("CF is required")
                .Length(16).WithMessage("CF must be 16 characters")
                .Matches("^[A-Z]{6}\\d{2}[A-Z]\\d{2}[A-Z]\\d{3}[A-Z]$").WithMessage("CF not valid");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("The password must be at least 8 characters")
                .MinimumLength(8).WithMessage("The password must be at least 8 characters")
                .MaximumLength(72).WithMessage("The password must not exceed 72 characters");

            RuleFor(x => x.PasswordConfirmation)
                .NotEmpty().WithMessage("The password confirmation is required")
                .Equal(x => x.Password).WithMessage("The passwords do not match");

            RuleFor(x => x.BirthDate)
                .Must(d => !d.HasValue || d.Value <= DateOnly.FromDateTime(DateTime.Now))
                .WithMessage("BirthDate cannot be in the future");
        }
    }
}
